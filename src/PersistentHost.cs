using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

class HostRequest { public string Action="status",Kind="server",Exe="",Arguments="",Directory="",Text="";public long Since;public int Heap;public bool BootRequest,ScheduleRequest; }
class HostLine { public long Sequence;public string Text; }
class HostReply { public int Protocol=1,ServerPid,TunnelPid,Heap,ServerExitCode=-1;public long ServerStart,TunnelStart,Sequence;public bool Ready,BootInitializing,Maintenance,ExpectedStop;public string Session="",Error="",ScheduleWarning="";public HostLine[] Lines=new HostLine[0]; }

partial class CraftHarbor
{
    static string HostPipe(string path){return "CraftHarbor-"+WindowsIdentity.GetCurrent().User.Value.Replace('-','_')+"-"+WorkspaceMutexName(path).Substring(6);}
    static readonly object hostLock=new object();
    static Process hostedServer,hostedTunnel;
    static volatile bool hostedReady,hostExit,hostedExpectedStop;
    static readonly string hostSession=Guid.NewGuid().ToString("N");
    string attachedHostSession="";
    static int hostedHeap;
    static long hostSequence,hostedServerStart;
    static readonly Queue<HostLine> hostLines=new Queue<HostLine>();
    long hostCursor;
    bool persistentAttached,scheduledMaintenance,intentionalHostStop;
    int hostPolling;
    int serverExitCode=-1;
    static void HostLog(string text){lock(hostLock){hostLines.Enqueue(new HostLine{Sequence=++hostSequence,Text=text});while(hostLines.Count>2000)hostLines.Dequeue();}}
    static HostReply HostStatus(long since){lock(hostLock){var reply=new HostReply{Session=hostSession,ServerStart=hostedServerStart,ScheduleWarning=scheduleCancellationWarning,Ready=Running(hostedServer)&&hostedReady,BootInitializing=bootInitializing,Maintenance=scheduleBusy,ExpectedStop=hostedExpectedStop,Heap=hostedHeap,Sequence=hostSequence,Lines=hostLines.Where(l=>l.Sequence>since).ToArray()};if(Running(hostedServer)){reply.ServerPid=hostedServer.Id;reply.ServerStart=hostedServer.StartTime.ToUniversalTime().Ticks;}if(hostedServer!=null&&hostedServer.HasExited)reply.ServerExitCode=hostedServer.ExitCode;if(Running(hostedTunnel)){reply.TunnelPid=hostedTunnel.Id;reply.TunnelStart=hostedTunnel.StartTime.ToUniversalTime().Ticks;}return reply;}}
    static HostReply ExecuteHost(HostRequest request,string workspace)
    {
        lock(hostLock) return ExecuteHostLocked(request,workspace);
    }
    static HostReply ExecuteHostLocked(HostRequest request,string workspace)
    {
        if(request==null)throw new InvalidDataException("管理操作を読み取れません。");
        if(request.Action=="cancel-schedule"){CancelScheduledOperation(workspace);return HostStatus(request.Since);}
        if(request.Action=="cancel-boot"){bootCancelled=true;return HostStatus(request.Since);}
        if(request.Action=="start"){
            if(scheduleBusy&&!request.ScheduleRequest&&request.Kind=="server")throw new InvalidOperationException("予約の保存・バックアップ・再起動中です。中断または完了を待ってください。");
            if(hostExit)throw new InvalidOperationException("常駐プロセスは終了中です。");
            if(bootInitializing&&!request.BootRequest)throw new InvalidOperationException("Windows起動時の準備中です。完了を待つか、運用画面で自動復帰を中断してください。");
            if(request.BootRequest&&(bootCancelled||(request.Kind!="server"&&bootTunnelCancelled)))throw new InvalidOperationException("自動復帰を中断しました。");
            bool mc=request.Kind=="server";if(Running(mc?hostedServer:hostedTunnel))return HostStatus(request.Since);
            if(!Path.IsPathRooted(request.Directory)||!Directory.Exists(request.Directory))throw new IOException("起動フォルダーを確認してください。");
            var process=new Process{StartInfo=new ProcessStartInfo(request.Exe,request.Arguments){WorkingDirectory=request.Directory,UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8}};
            string label=mc?"MC":"公開";DataReceivedEventHandler read=delegate(object s,DataReceivedEventArgs e){if(e.Data==null)return;string line=System.Text.RegularExpressions.Regex.Replace(e.Data,@"\x1B\[[0-?]*[ -/]*[@-~]","");line=System.Text.RegularExpressions.Regex.Replace(line,"§.","");if(mc)ObserveScheduledPlayers(line);if(mc&&line.Contains("Done (")&&line.Contains("For help"))hostedReady=true;HostLog("["+label+"] "+line);};process.OutputDataReceived+=read;process.ErrorDataReceived+=read;process.Start();if(mc){hostedServer=process;hostedServerStart=process.StartTime.ToUniversalTime().Ticks;hostedReady=false;hostedExpectedStop=false;scheduledPlayers=-1;scheduledPlayersAt=DateTime.MinValue;hostedHeap=request.Heap;}else hostedTunnel=process;process.BeginOutputReadLine();process.BeginErrorReadLine();HostLog("[常駐] "+(mc?"サーバー":"公開接続")+"を独立プロセスで起動しました。");
        }else if(request.Action=="command"){
            if(request.Text.Trim().Equals("stop",StringComparison.OrdinalIgnoreCase)){bootCancelled=true;hostedExpectedStop=true;if(!request.ScheduleRequest)CancelScheduledOperation(workspace);}
            if(!Running(hostedServer))throw new InvalidOperationException("サーバーは停止しています。");if(request.Text.Length>32768)throw new InvalidDataException("コマンドが長すぎます。");hostedServer.StandardInput.WriteLine(request.Text);hostedServer.StandardInput.Flush();
        }else if(request.Action=="stop-tunnel"){
            bootTunnelCancelled=true;if(Running(hostedTunnel)){hostedTunnel.Kill();hostedTunnel.WaitForExit(5000);}HostLog("[常駐] 公開接続を停止しました。");
        }else if(request.Action=="shutdown-idle"){if(bootInitializing||scheduleBusy||Running(hostedServer)||Running(hostedTunnel))throw new InvalidOperationException("稼働中のサーバーがあるため常駐プロセスは終了できません。");hostExit=true;}
        else if(request.Action!="status")throw new InvalidDataException("不明な管理操作です。");
        return HostStatus(request.Since);
    }
    static int PersistentHostMain(string[] args)
    {
        string workspace=null;for(int i=0;i<args.Length-1;i++)if(args[i]=="--workspace")workspace=Path.GetFullPath(args[i+1]);if(workspace==null||!ValidWorkspace(workspace))return 2;string pipe=HostPipe(workspace);
        bootManaged=Array.IndexOf(args,"--boot-start")>=0;
        if(bootManaged&&!BootOwnerMatches(BootArgument(args,"--owner-sid")))return 3;
        bool first;using(var mutex=new Mutex(true,"Global\\"+pipe+"-host",out first)){
            if(!first)return 0;var security=new PipeSecurity();var user=WindowsIdentity.GetCurrent().User;security.SetAccessRuleProtection(true,false);security.SetOwner(user);security.AddAccessRule(new PipeAccessRule(user,PipeAccessRights.FullControl,AccessControlType.Allow));
            using(var installGuard=new Mutex(false,"Global\\CraftHarborInstallerGuard")){
            if(bootManaged){bootInitializing=true;Task.Run(delegate{RunBootRecovery(workspace);});}
            Task.Run(delegate{RunOperationSchedule(workspace);});
            while(!hostExit){
                using(var serverPipe=new NamedPipeServerStream(pipe,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.None,65536,65536,security)){
                    serverPipe.WaitForConnection();try{using(var reader=new StreamReader(serverPipe,new UTF8Encoding(false),false,4096,true))using(var writer=new StreamWriter(serverPipe,new UTF8Encoding(false),4096,true){AutoFlush=true}){
                        string line=reader.ReadLine();if(line==null||line.Length>1048576)continue;HostReply reply;try{reply=ExecuteHost(new JavaScriptSerializer().Deserialize<HostRequest>(line),workspace);}catch(Exception e){reply=HostStatus(0);reply.Error=e.Message;}writer.WriteLine(new JavaScriptSerializer{MaxJsonLength=4194304}.Serialize(reply));
                    }}catch(IOException){}catch(InvalidDataException){}
                }
            }
            }
        }
        return 0;
    }
    HostReply HostCall(HostRequest request,int timeout=300)
    {
        using(var pipe=new NamedPipeClientStream(".",HostPipe(root),PipeDirection.InOut,PipeOptions.None)){
            pipe.Connect(timeout);using(var writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true){AutoFlush=true})using(var reader=new StreamReader(pipe,new UTF8Encoding(false),false,4096,true)){
                writer.WriteLine(new JavaScriptSerializer().Serialize(request));string json=reader.ReadLine();if(json==null||json.Length>4194304)throw new IOException("常駐プロセスの応答を確認してください。");var reply=new JavaScriptSerializer{MaxJsonLength=4194304}.Deserialize<HostReply>(json);if(reply.Protocol!=1)throw new IOException("常駐プロセスとのバージョンが一致しません。");if(reply.Error.Length>0)throw new InvalidOperationException(reply.Error);return reply;
            }
        }
    }
    void EnsurePersistentHost()
    {
        try{HostCall(new HostRequest());persistentAttached=true;return;}catch(TimeoutException){}catch(IOException){}
        string exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CraftHarbor.Host.exe");if(!File.Exists(exe))throw new FileNotFoundException("CraftHarbor.Host.exeを管理アプリと同じフォルダーへ配置してください。");
        Process.Start(new ProcessStartInfo(exe,"--workspace "+Q(root)){UseShellExecute=false,CreateNoWindow=true});for(int i=0;i<30;i++){try{HostCall(new HostRequest(),100);persistentAttached=true;return;}catch(TimeoutException){}catch(IOException){}Thread.Sleep(100);}throw new IOException("常駐プロセスを開始できませんでした。");
    }
    static Process HostProcess(int id,long started){if(id<=0)return null;try{var p=Process.GetProcessById(id);if(!p.HasExited&&p.StartTime.ToUniversalTime().Ticks==started)return p;p.Dispose();}catch(ArgumentException){}catch(InvalidOperationException){}return null;}
    void AcceptHostStatus(HostReply reply)
    {
        if(attachedHostSession!=reply.Session){hostCursor=0;attachedHostSession=reply.Session;}serverExitCode=reply.ServerExitCode;bootPreparing=reply.BootInitializing;scheduledMaintenance=reply.Maintenance;intentionalHostStop=reply.ExpectedStop;scheduleWarning=reply.ScheduleWarning??"";
        if(reply.ServerPid>0&&(server==null||server.Id!=reply.ServerPid))server=HostProcess(reply.ServerPid,reply.ServerStart);if(reply.TunnelPid>0&&(tunnel==null||tunnel.Id!=reply.TunnelPid))tunnel=HostProcess(reply.TunnelPid,reply.TunnelStart);ready=reply.Ready;activeHeapGiB=reply.Heap;
        foreach(var line in reply.Lines){if(line.Sequence<=hostCursor)continue;if(line.Text.StartsWith("[MC] "))ParseServerMetrics(line.Text.Substring(5));if(line.Text.StartsWith("[公開] ")){var claim=System.Text.RegularExpressions.Regex.Match(line.Text,@"https://playit\.gg/claim/[A-Za-z0-9_-]+");if(claim.Success)claimUrl=claim.Value;var endpoint=System.Text.RegularExpressions.Regex.Match(line.Text,@"tcp://([A-Za-z0-9.-]+:\d+)");if(endpoint.Success&&hosting.Provider=="ngrok")discoveredGame=endpoint.Groups[1].Value;}Add(line.Text);}hostCursor=reply.Sequence;
    }
    void AttachPersistentHost(){try{var reply=HostCall(new HostRequest());persistentAttached=true;AcceptHostStatus(reply);if(reply.ServerPid>0){previousRunning=true;readyNotified=reply.Ready;Add("[常駐] 稼働中のサーバーへ再接続しました。");}}catch(TimeoutException){}catch(IOException){}}
    void PollPersistentHost(bool synchronous=false)
    {
        if(!persistentAttached||Interlocked.CompareExchange(ref hostPolling,1,0)!=0)return;Action poll=delegate{try{AcceptHostStatus(HostCall(new HostRequest{Since=hostCursor}));}catch(Exception e){Add("[常駐] "+e.Message);}finally{Interlocked.Exchange(ref hostPolling,0);}};if(synchronous)poll();else Task.Run(poll);
    }
    Process LaunchPersistent(string kind,string exe,string arguments,string directory,int heap)
    {
        EnsurePersistentHost();var reply=HostCall(new HostRequest{Action="start",Kind=kind,Exe=exe,Arguments=arguments,Directory=directory,Heap=heap,Since=hostCursor},2000);AcceptHostStatus(reply);return HostProcess(kind=="server"?reply.ServerPid:reply.TunnelPid,kind=="server"?reply.ServerStart:reply.TunnelStart);
    }
}
