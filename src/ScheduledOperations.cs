using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class OperationSchedule
{
    public bool Enabled;
    public string Time = "05:00";
}
class ScheduleRunState
{
    public string Day = "", Phase = "", Message = "", UpdatedUtc = "", Backup = "";
    // Claim before any destructive action. An interrupted host never replays an ambiguous run.
    public bool Claimed;
}
partial class CraftHarbor
{
    static volatile bool scheduleBusy;
    static long scheduleGeneration;
    static string scheduleCancelledDay="",scheduleCancellationWarning="";
    string scheduleWarning="",scheduleCommunicationWarning="";
    bool HasScheduleWarning {get{return scheduleWarning.Length>0||scheduleCommunicationWarning.Length>0;}}
    static int scheduledPlayers = -1;
    static DateTime scheduledPlayersAt = DateTime.MinValue, scheduleNextCheck = DateTime.MinValue;
    Label scheduleStatus;

    static string ScheduleStatePath(string workspace) { return Path.Combine(workspace,"operation-schedule-state.json"); }
    static OperationSchedule ReadOperationSchedule(string workspace)
    {
        var values = ReadBootJson<Dictionary<string,object>>(Path.Combine(workspace,"manager-options.json"));
        object enabled, time;
        var result = new OperationSchedule();
        if(values != null && values.TryGetValue("scheduledRestart",out enabled)) result.Enabled = Convert.ToBoolean(enabled);
        if(values != null && values.TryGetValue("restartTime",out time)) result.Time = Convert.ToString(time);
        DateTime parsed;
        if(!DateTime.TryParseExact(result.Time,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out parsed))
            throw new InvalidDataException("予約時刻をHH:mm形式で確認してください。");
        return result;
    }
    static DateTime ScheduleTarget(DateTime now, OperationSchedule options)
    {
        DateTime parsed;
        if(!DateTime.TryParseExact(options.Time,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out parsed))
            throw new InvalidDataException("予約時刻が無効です。");
        return now.Date.Add(parsed.TimeOfDay);
    }
    static DateTime ScheduleOccurrenceTarget(DateTime now,OperationSchedule options,ScheduleRunState previous)
    {
        DateTime previousDay;
        // Retain a deferred occurrence across midnight, including its final expiry record. Once
        // it is claimed/skipped, the following tick can consider today's independent occurrence.
        if(!previous.Claimed&&previous.Phase=="deferred"&&
            DateTime.TryParseExact(previous.Day,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out previousDay)&&previousDay<now.Date)
            return ScheduleTarget(previousDay,options);
        return ScheduleTarget(now,options);
    }
    static string ScheduleDecision(DateTime now,OperationSchedule options,ScheduleRunState previous,bool running,bool ready)
    {
        if(!options.Enabled) return "disabled";
        string day = now.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        if(string.CompareOrdinal(previous.Day,day)>0 || previous.Day == day && previous.Claimed) return "consumed";
        DateTime target = ScheduleOccurrenceTarget(now,options,previous);
        if(now < target) return "early";
        if(now >= target.AddMinutes(30)) return "expired";
        return running && ready ? "query" : "defer";
    }
    static ScheduleRunState ReadScheduleState(string workspace)
    {
        var state=ReadBootJson<ScheduleRunState>(ScheduleStatePath(workspace));
        DateTime day;
        if(state==null || state.Day==null || state.Phase==null || state.Message==null ||
            state.Day.Length>0&&!DateTime.TryParseExact(state.Day,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day) ||
            state.Claimed&&state.Day.Length==0)
            throw new InvalidDataException("予約の実行記録が不正です。自動再実行せず記録を確認してください。");
        return state;
    }
    static void WriteScheduleState(string workspace,ScheduleRunState state)
    {
        state.UpdatedUtc = DateTime.UtcNow.ToString("o");
        AtomicManagerWrite(ScheduleStatePath(workspace),new JavaScriptSerializer().Serialize(state));
        HostLog("[予約] " + state.Message);
    }
    static ScheduleRunState ScheduleReport(string workspace,string day,string phase,string message,bool claimed,string backup)
    {
        if(!Monitor.IsEntered(hostLock))throw new InvalidOperationException("予約記録の排他が必要です。");
        var state = new ScheduleRunState{Day=day,Phase=phase,Message=message,Claimed=claimed,Backup=backup??""};
        WriteScheduleState(workspace,state);return state;
    }
    static bool ReportScheduledRun(string workspace,string day,long generation,string phase,string message,bool claimed,string backup)
    {
        // The check and durable write are one host transaction. A delayed worker must never
        // overwrite a manual cancellation with an unclaimed/deferred or misleading success state.
        lock(hostLock){
            if(scheduleCancelledDay==day||ScheduleCancelled(workspace,generation))return false;
            ScheduleReport(workspace,day,phase,message,claimed,backup);return true;
        }
    }
    static void CancelScheduledOperation(string workspace)
    {
        // Called with hostLock held, including while a backup or preflight is running.
        scheduleGeneration++;scheduleCancelledDay=DateTime.Now.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        try{ScheduleReport(workspace,scheduleCancelledDay,"cancelled",
            "利用者の停止操作を優先しました。本日の予約は再実行しません。",true,"");scheduleCancellationWarning="";}
        catch(IOException){RecordScheduleCancellationWarning();}
        catch(UnauthorizedAccessException){RecordScheduleCancellationWarning();}
    }
    static void RecordScheduleCancellationWarning()
    {
        scheduleCancellationWarning="現ホストで予約を中断しましたが、中断記録の永続保存に失敗しました。ホスト・PC再起動後は予約状態を再確認してください。";
        HostLog("[予約警告] "+scheduleCancellationWarning);
    }
    bool CancelScheduledFromUi(bool explicitRequest=true)
    {
        // A settings-only save with no known host changes the durable enabled flag; it does not
        // claim that cancellation was acknowledged. Explicit stop/cancel always probes IPC.
        if(!explicitRequest&&!persistentAttached)return !HasScheduleWarning;
        try{
            // Cached attachment is not proof of host absence. A timed-out initial attach must not
            // turn an unsent cancellation into an acknowledgement. Never start a host here.
            var reply=HostCall(new HostRequest{Action="cancel-schedule"},persistentAttached?2000:300);
            persistentAttached=true;AcceptHostStatus(reply);
            scheduleCommunicationWarning="";scheduleWarning=reply.ScheduleWarning??"";
        }catch(Exception error){
            if(!(error is IOException)&&!(error is TimeoutException)&&!(error is InvalidOperationException)&&!(error is UnauthorizedAccessException))throw;
            // A failed cancellation acknowledgement must not prevent the following graceful stop
            // request. Its uncertainty is kept visible until a cancellation is acknowledged.
            scheduleCommunicationWarning="予約中断の応答を確認できません。停止要求は続行しますが、管理ログと予約状態を確認してください。";
        }
        if(HasScheduleWarning)Add("[予約警告] "+scheduleCommunicationWarning+scheduleWarning);
        RefreshScheduleStatus();return !HasScheduleWarning;
    }
    static void ObserveScheduledPlayers(string line)
    {
        // Only a complete command response is evidence. Never match text embedded in player chat,
        // '/say', logger prefixes or arbitrary plugin output. Unrecognised server formats stay unknown.
        string text=Regex.Replace(line,@"^\[\d{2}:\d{2}:\d{2}\] \[(?:Server thread|ServerMain)/INFO\](?: \[(?:minecraft/)?(?:DedicatedServer|MinecraftServer)\])?:\s*","");
        text=Regex.Replace(text,@"^\[\d{2}:\d{2}:\d{2} INFO\]:\s*","");
        var match=Regex.Match(text,@"^There are (\d+) of a max of (\d+) players online:\s*([A-Za-z0-9_]*(?:,\s*[A-Za-z0-9_]+)*)\s*$");
        int count,max;
        if(match.Success&&int.TryParse(match.Groups[1].Value,out count)&&int.TryParse(match.Groups[2].Value,out max)&&count<=max&&max>0&&
            (count>0||match.Groups[3].Value.Trim().Length==0))
            lock(hostLock){scheduledPlayers=count;scheduledPlayersAt=DateTime.UtcNow;}
    }

    static bool ScheduleCancelled(string workspace,long generation)
    {
        lock(hostLock) if(hostExit || scheduleGeneration!=generation) return true;
        try{return !ReadOperationSchedule(workspace).Enabled;}catch{return true;}
    }
    static int QueryScheduledPlayers(string workspace,long generation,Process expected)
    {
        DateTime sent;
        lock(hostLock){
            if(ScheduleCancelled(workspace,generation) || !Running(expected) || hostedServer!=expected || !hostedReady)return -1;
            // A stale or unsolicited response from before this request is never treated as an empty server.
            scheduledPlayers=-1;scheduledPlayersAt=DateTime.MinValue;sent=DateTime.UtcNow;
            expected.StandardInput.WriteLine("list");expected.StandardInput.Flush();
        }
        DateTime deadline=DateTime.UtcNow.AddSeconds(10);
        while(DateTime.UtcNow<deadline){
            if(ScheduleCancelled(workspace,generation))return -1;
            lock(hostLock){
                if(!Running(expected)||hostedServer!=expected)return -1;
                if(scheduledPlayersAt>=sent && (DateTime.UtcNow-scheduledPlayersAt).TotalSeconds<=10)return scheduledPlayers;
            }
            Thread.Sleep(100);
        }
        return -1;
    }
    static bool WaitScheduled(string workspace,long generation,TimeSpan delay)
    {
        DateTime end=DateTime.UtcNow.Add(delay);
        while(DateTime.UtcNow<end){if(ScheduleCancelled(workspace,generation))return false;Thread.Sleep(100);}
        return !ScheduleCancelled(workspace,generation);
    }
    static long ScheduleBackupBytes(string directory)
    {
        if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new IOException("リンク先を含むサーバーは予約バックアップできません。");
        long bytes=0;
        foreach(string file in Directory.GetFiles(directory)){
            if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("リンク先を含むサーバーは予約バックアップできません。");
            bytes=checked(bytes+new FileInfo(file).Length);
        }
        foreach(string child in Directory.GetDirectories(directory)) bytes=checked(bytes+ScheduleBackupBytes(child));
        return bytes;
    }
    static string CreateScheduledBackup(string workspace)
    {
        string directory=Path.Combine(workspace,"server");
        CheckBootPort(directory);
        long bytes=ScheduleBackupBytes(directory);
        if(new DriveInfo(Path.GetPathRoot(workspace)).AvailableFreeSpace<checked(bytes+512L*1024*1024))
            throw new IOException("完全バックアップ用のディスク空き容量が不足しています。サーバーは停止したままです。");
        string folder=Path.Combine(workspace,"backups");Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"scheduled-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")+".zip");
        ZipFile.CreateFromDirectory(directory,path+".partial",CompressionLevel.Fastest,false);
        File.Move(path+".partial",path);
        return path;
    }
    static void RunScheduledRestart(string workspace,string day,DateTime latestStart,long generation,Process expected)
    {
        string backupPath="";
        try{
            if(!ReportScheduledRun(workspace,day,generation,"announced","無人を確認しました。60秒後に再確認し、保存・停止・完全バックアップ後に再起動します。",true,""))return;
            lock(hostLock){
                if(ScheduleCancelled(workspace,generation))return;
                expected.StandardInput.WriteLine("say [Server] 60 seconds until scheduled maintenance. Restart is deferred while players are online.");expected.StandardInput.Flush();
            }
            if(!WaitScheduled(workspace,generation,TimeSpan.FromSeconds(60)))return;
            int players=QueryScheduledPlayers(workspace,generation,expected);
            if(ScheduleCancelled(workspace,generation))return;
            if(players!=0){
                ReportScheduledRun(workspace,day,generation,"deferred",players<0?"参加人数を確認できないため延期しました。予定から30分を超えると本日は実行しません。":"参加者がいるため延期しました。予定から30分を超えると本日は実行しません。",false,"");return;
            }
            if(DateTime.Now>=latestStart){
                ReportScheduledRun(workspace,day,generation,"skipped","延期上限の30分を超えたため本日の予約を見送りました。",true,"");return;
            }
            using(var maintenance=WorkspaceMaintenanceLock(workspace)){
                lock(hostLock){
                    if(ScheduleCancelled(workspace,generation)||!Running(expected)||hostedServer!=expected)return;
                    scheduleBusy=true;hostedExpectedStop=true;
                    if(!ReportScheduledRun(workspace,day,generation,"stopping","無人を再確認しました。ワールドを保存して停止しています。",true,""))return;
                    // 'stop' performs the server's own graceful save. Never kill a server that is still saving.
                    expected.StandardInput.WriteLine("save-all flush");expected.StandardInput.WriteLine("stop");expected.StandardInput.Flush();
                }
                DateTime stopDeadline=DateTime.UtcNow.AddMinutes(2);
                while(Running(expected)&&DateTime.UtcNow<stopDeadline){if(ScheduleCancelled(workspace,generation))return;Thread.Sleep(100);}
                if(Running(expected))throw new IOException("停止に時間がかかっています。保存処理を強制終了せず、再起動を中止しました。");
                expected.WaitForExit();
                if(expected.ExitCode!=0)throw new IOException("保存停止の終了コードを確認できません。再起動せず停止を維持します。");
                if(ScheduleCancelled(workspace,generation))return;
                if(!ReportScheduledRun(workspace,day,generation,"backup","停止後のサーバー全体をバックアップしています。",true,""))return;
                backupPath=CreateScheduledBackup(workspace);
                if(ScheduleCancelled(workspace,generation))return;
                using(var gate=new Mutex(false,MemoryGateName())){
                    bool acquired=false;
                    try{
                        DateTime gateDeadline=DateTime.UtcNow.AddSeconds(90);
                        while(!acquired&&DateTime.UtcNow<gateDeadline){
                            if(ScheduleCancelled(workspace,generation))return;
                            try{acquired=gate.WaitOne(100);}catch(AbandonedMutexException){acquired=true;}
                        }
                        if(!acquired)throw new IOException("メモリ予算の排他待ちが上限に達しました。再起動せず停止を維持します。");
                        if(ScheduleCancelled(workspace,generation))return;
                        var request=BuildBootServerRequest(workspace);request.BootRequest=false;request.ScheduleRequest=true;
                        lock(hostLock){
                            if(ScheduleCancelled(workspace,generation))return;
                            ExecuteHostLocked(request,workspace);
                            try{ReserveWorkspaceMemory(workspace,hostedServer,request.Heap);}
                            catch{if(Running(hostedServer)){hostedExpectedStop=true;hostedServer.StandardInput.WriteLine("stop");hostedServer.StandardInput.Flush();}throw;}
                        }
                    }finally{if(acquired)gate.ReleaseMutex();}
                }
            }
            DateTime readyDeadline=DateTime.UtcNow.AddMinutes(10);
            while(!hostedReady&&Running(hostedServer)&&DateTime.UtcNow<readyDeadline){if(ScheduleCancelled(workspace,generation))return;Thread.Sleep(100);}
            if(ScheduleCancelled(workspace,generation))return;
            if(!Running(hostedServer)||!hostedReady)throw new IOException("再起動完了を確認できません。自動再試行せず、管理画面のログを確認してください。");
            ReportScheduledRun(workspace,day,generation,"complete","保存・完全バックアップ・再起動が完了しました。",true,backupPath);
        }catch(Exception error){
            if(!ScheduleCancelled(workspace,generation))ReportScheduledRun(workspace,day,generation,"error","予約処理を中止しました。自動再試行しません："+error.Message,true,backupPath);
        }finally{
            lock(hostLock){scheduleBusy=false;}
            // Disabling the reservation is also a cancellation, but it need not send a host command.
            try{
                lock(hostLock){
                    if(scheduleGeneration==generation&&scheduleCancelledDay!=day&&ScheduleCancelled(workspace,generation))
                        ScheduleReport(workspace,day,"cancelled","予約が無効・読取不能になったため中断しました。自動再起動しません。",true,backupPath);
                }
            }catch(Exception error){HostLog("[予約] 中断しましたが最終記録を保存できませんでした："+error.Message);}
        }
    }
    static void TickOperationSchedule(string workspace)
    {
        if(DateTime.UtcNow<scheduleNextCheck)return;
        scheduleNextCheck=DateTime.UtcNow.AddSeconds(30);
        var options=ReadOperationSchedule(workspace);
        var previous=ReadScheduleState(workspace);
        Process current;long generation;bool isReady;
        lock(hostLock){current=hostedServer;generation=scheduleGeneration;isReady=hostedReady&&!bootInitializing&&!scheduleBusy;}
        DateTime now=DateTime.Now;DateTime target=ScheduleOccurrenceTarget(now,options,previous);
        string day=target.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        if(scheduleCancelledDay==day||scheduleCancelledDay==now.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture))return;
        string decision=ScheduleDecision(now,options,previous,Running(current),isReady);
        if(decision=="expired"){
            ReportScheduledRun(workspace,day,generation,"skipped","参加者あり・未確認・未起動の延期上限30分を超えたため本日の予約を見送りました。",true,"");return;
        }
        if(decision=="defer"){
            if(previous.Day!=day||previous.Phase!="deferred")
                ReportScheduledRun(workspace,day,generation,"deferred","サーバーの準備・稼働状態が未確認のため延期しています。",false,"");
            return;
        }
        if(decision!="query")return;
        int players=QueryScheduledPlayers(workspace,generation,current);
        if(ScheduleCancelled(workspace,generation))return;
        if(players!=0){
            if(previous.Day!=day||previous.Phase!="deferred")
                ReportScheduledRun(workspace,day,generation,"deferred",players<0?"参加人数が不明のため延期しています。無人の新しい応答が必要です。":"参加者がいるため延期しています。",false,"");
            return;
        }
        RunScheduledRestart(workspace,day,target.AddMinutes(30),generation,current);
    }
    static void RunOperationSchedule(string workspace)
    {
        string lastError="";
        while(!hostExit){
            try{TickOperationSchedule(workspace);lastError="";}
            catch(Exception error){if(error.Message!=lastError){HostLog("[予約] 設定・記録を確認してください。安全のため実行しません："+error.Message);lastError=error.Message;}}
            Thread.Sleep(1000);
        }
    }
    void AddScheduleControls(FlowLayoutPanel automation)
    {
        var actions=new FlowLayoutPanel{AutoSize=true};
        FlowBtn(actions,"予約処理を中断",delegate{if(CancelScheduledFromUi())Add(persistentAttached?"[予約] 本日の予約処理の中断記録を確認しました。":"[予約] 接続中の常駐ホストはありません。予約の有効・無効は運用設定で保存してください。");RefreshScheduleStatus();});automation.Controls.Add(actions);
        scheduleStatus=new Label{AutoSize=true,MaximumSize=new Size(950,0),ForeColor=Muted,AccessibleName="予約処理の最終結果"};automation.Controls.Add(scheduleStatus);RefreshScheduleStatus();
    }
    void RefreshScheduleStatus()
    {
        if(scheduleStatus==null)return;
        try{
            var status=ReadScheduleState(root);
            string warning=HasScheduleWarning?"警告: "+scheduleCommunicationWarning+scheduleWarning+"\r\n":"";
            scheduleStatus.Text=warning+(string.IsNullOrEmpty(status.Message)?"予約の実行記録はまだありません。常駐ホストとサーバーが動いている間だけ実行します。":status.UpdatedUtc+" / "+status.Message);
        }catch(Exception){scheduleStatus.Text=(HasScheduleWarning?"警告: "+scheduleCommunicationWarning+scheduleWarning+"\r\n":"")+"予約の実行記録を読み取れません。安全のため実行状態を管理ログで確認してください。";}
    }
}
