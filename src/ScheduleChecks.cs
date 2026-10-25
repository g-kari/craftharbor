using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

partial class CraftHarbor
{
    static void RunScheduleChecks()
    {
        var enabled=new OperationSchedule{Enabled=true,Time="05:00"};
        DateTime day=new DateTime(2026,10,25,5,0,0);
        Require(ScheduleDecision(day.AddSeconds(-1),enabled,new ScheduleRunState(),true,true)=="early","reservation never runs before local target");
        Require(ScheduleDecision(day,enabled,new ScheduleRunState(),true,true)=="query","ready server requires a fresh participant query");
        Require(ScheduleDecision(day.AddMinutes(29),enabled,new ScheduleRunState(),true,false)=="defer","unready state is not interpreted as empty");
        Require(ScheduleDecision(day.AddMinutes(30),enabled,new ScheduleRunState(),true,true)=="expired","thirty-minute delay window is bounded");
        Require(ScheduleDecision(day,enabled,new ScheduleRunState{Day="2026-10-25",Claimed=true},true,true)=="consumed","claimed occurrence never replays after host restart");
        Require(ScheduleDecision(day.AddDays(1),enabled,new ScheduleRunState{Day="2026-10-25",Claimed=true},true,true)=="query","next calendar day is a new occurrence");
        Require(ScheduleDecision(day,enabled,new ScheduleRunState{Day="2026-10-26",Claimed=true},true,true)=="consumed","clock rollback cannot replay an earlier occurrence");
        Require(ScheduleDecision(day,new OperationSchedule{Enabled=false,Time="05:00"},new ScheduleRunState(),true,true)=="disabled","reservation opt-out");
        bool invalid=false;try{ScheduleTarget(day,new OperationSchedule{Time="25:99"});}catch(InvalidDataException){invalid=true;}
        Require(invalid,"invalid time cannot run maintenance");
        var late=new OperationSchedule{Enabled=true,Time="23:50"};
        Require(ScheduleTarget(day,late).AddMinutes(30).Date==day.Date.AddDays(1),"delay deadline retains occurrence date across midnight");
        var previousNight=new ScheduleRunState{Day="2026-10-25",Phase="deferred",Claimed=false};
        DateTime afterMidnight=new DateTime(2026,10,26,0,1,0);
        Require(ScheduleOccurrenceTarget(afterMidnight,late,previousNight)==new DateTime(2026,10,25,23,50,0),"deferred occurrence retains the prior-night target");
        Require(ScheduleDecision(afterMidnight,late,previousNight,true,true)=="query","prior-night reservation retries after midnight within thirty minutes");
        Require(ScheduleDecision(afterMidnight.AddMinutes(19),late,previousNight,true,true)=="expired","prior-night reservation expires at its original deadline");
        Require(ScheduleDecision(afterMidnight,late,new ScheduleRunState{Day="2026-10-26",Phase="cancelled",Claimed=true},true,true)=="consumed","manual cancellation after midnight suppresses both pending and current-day reservation");
        Require(ScheduleDecision(afterMidnight,late,new ScheduleRunState{Day="2026-10-25",Phase="skipped",Claimed=true},true,true)=="early","finished prior-night occurrence does not consume the next day's schedule");
        int before=scheduledPlayers;DateTime stamp=scheduledPlayersAt;
        ObserveScheduledPlayers("There are 3 of a max of 20 players online: fixture");
        Require(scheduledPlayers==3&&scheduledPlayersAt>DateTime.UtcNow.AddSeconds(-5),"participant evidence parsed and timestamped in host");
        ObserveScheduledPlayers("There are 99999999999999999 of a max of 20 players online:");
        Require(scheduledPlayers==3,"malformed participant response cannot imply empty");
        DateTime accepted=scheduledPlayersAt;
        foreach(string fake in new[]{"<Alex> There are 0 of a max of 20 players online:","[05:00:00] [Server thread/INFO]: <Alex> There are 0 of a max of 20 players online:","[05:00:00] [Server thread/INFO]: [Server] There are 0 of a max of 20 players online:","prefix There are 0 of a max of 20 players online:","There are 0 of a max of 20 players online: Alex"}){
            ObserveScheduledPlayers(fake);Require(scheduledPlayers==3&&scheduledPlayersAt==accepted,"chat or embedded text is not participant evidence");
        }
        ObserveScheduledPlayers("[05:00:00] [Server thread/INFO]: There are 0 of a max of 20 players online:");
        Require(scheduledPlayers==0,"complete known console response accepted");
        scheduledPlayers=before;scheduledPlayersAt=stamp;
        RunScheduleCancellationChecks();
    }
    static void RunScheduleCancellationChecks()
    {
        string scratch=Path.Combine(Path.GetTempPath(),"CraftHarbor-schedule-cas-"+Guid.NewGuid().ToString("N"));
        string oldData=ManagerDataOverride,oldDay=scheduleCancelledDay,oldWarning=scheduleCancellationWarning;long oldGeneration=scheduleGeneration;bool oldExit=hostExit;
        Directory.CreateDirectory(scratch);ManagerDataOverride=Path.Combine(scratch,"manager-data");
        try{
            foreach(bool cancelFirst in new[]{true,false}){
                string workspace=Path.Combine(scratch,cancelFirst?"cancel-first":"write-first");Directory.CreateDirectory(workspace);
                AtomicManagerWrite(Path.Combine(workspace,"manager-options.json"),"{\"scheduledRestart\":true,\"restartTime\":\"05:00\"}");
                string day=DateTime.Now.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
                lock(hostLock){scheduleGeneration=0;scheduleCancelledDay="";hostExit=false;}
                using(var arrived=new ManualResetEventSlim(false))using(var release=new ManualResetEventSlim(false)){
                    var worker=Task.Run(delegate{
                        if(cancelFirst){arrived.Set();if(!release.Wait(5000))throw new Exception("cancel-first barrier timed out");}
                        bool written=ReportScheduledRun(workspace,day,0,"deferred","synthetic delayed worker",false,"");
                        if(!cancelFirst){arrived.Set();if(!release.Wait(5000))throw new Exception("write-first barrier timed out");}
                        return written;
                    });
                    try{
                        Require(arrived.Wait(5000),"worker reached controlled report barrier");
                        if(cancelFirst)AtomicManagerWrite(Path.Combine(workspace,"manager-options.json"),"{\"scheduledRestart\":false,\"restartTime\":\"05:00\"}");
                        lock(hostLock){CancelScheduledOperation(workspace);}
                    }finally{release.Set();}
                    Require(worker.Wait(5000),"controlled worker terminates");
                    Require(worker.Result==!cancelFirst,"report/cancel ordering follows host generation transaction");
                }
                var state=ReadScheduleState(workspace);
                Require(state.Day==day&&state.Phase=="cancelled"&&state.Claimed,"manual cancellation remains durable and terminal");
                foreach(string phase in new[]{"deferred","announced","stopping","backup","complete","error","skipped"}){
                    Require(!ReportScheduledRun(workspace,day,0,phase,"stale worker",false,""),"stale "+phase+" report cannot overwrite cancellation");
                }
                state=ReadScheduleState(workspace);
                Require(state.Phase=="cancelled"&&state.Claimed,"stale worker cannot reopen a cancelled occurrence after host restart");
            }
            string disabled=Path.Combine(scratch,"disabled-file");Directory.CreateDirectory(disabled);
            string disabledDay=DateTime.Now.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
            lock(hostLock){scheduleGeneration=0;scheduleCancelledDay="";hostExit=false;}
            AtomicManagerWrite(Path.Combine(disabled,"manager-options.json"),"{\"scheduledRestart\":true,\"restartTime\":\"05:00\"}");
            Require(ReportScheduledRun(disabled,disabledDay,0,"announced","synthetic accepted claim",true,""),"enabled reservation can atomically claim occurrence");
            AtomicManagerWrite(Path.Combine(disabled,"manager-options.json"),"{\"scheduledRestart\":false,\"restartTime\":\"05:00\"}");
            Require(!ReportScheduledRun(disabled,disabledDay,0,"stopping","must not start stop",true,""),"configuration disable rechecked before stopping transaction");
            Require(ReadScheduleState(disabled).Claimed,"configuration update cannot reopen prior claim before cancellation acknowledgement");
            string blocked=Path.Combine(scratch,"cancel-write-failure");Directory.CreateDirectory(blocked);
            Directory.CreateDirectory(ScheduleStatePath(blocked));
            lock(hostLock){CancelScheduledOperation(blocked);}
            Require(scheduleCancellationWarning.Length>0,"cancellation storage failure is reported without blocking stop generation");
            Directory.Delete(ScheduleStatePath(blocked));
            lock(hostLock){CancelScheduledOperation(blocked);}
            Require(scheduleCancellationWarning.Length==0&&ReadScheduleState(blocked).Claimed,"successful durable cancellation clears prior storage warning");
        }finally{
            lock(hostLock){scheduleGeneration=oldGeneration;scheduleCancelledDay=oldDay;scheduleCancellationWarning=oldWarning;hostExit=oldExit;}
            ManagerDataOverride=oldData;
            Directory.Delete(scratch,true);
        }
    }
    static void WriteScheduleFixtureOptions(string workspace,bool enabled)
    {
        AtomicManagerWrite(Path.Combine(workspace,"manager-options.json"),new JavaScriptSerializer().Serialize(new Dictionary<string,object>{
            {"autoCrashRestart",true},{"scheduledRestart",enabled},{"restartTime",DateTime.Now.ToString("HH:mm",CultureInfo.InvariantCulture)},
            {"autoMemory",false},{"largeText",false},{"highContrast",false}}));
    }
    static void PrepareScheduleFixture(string workspace,string fixture)
    {
        Directory.CreateDirectory(Path.Combine(workspace,"server"));
        int port;using(var listener=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
            listener.Bind(new IPEndPoint(IPAddress.Loopback,0));port=((IPEndPoint)listener.LocalEndPoint).Port;
        }
        File.WriteAllText(Path.Combine(workspace,"server","server.jar"),"synthetic fixture, never a real JAR");
        File.WriteAllText(Path.Combine(workspace,"server","server.properties"),"server-port="+port+"\nview-distance=8\nsimulation-distance=6\nmotd=keep-me\n");
        File.WriteAllText(Path.Combine(workspace,"server","eula.txt"),"eula=true\n");
        Directory.CreateDirectory(Path.Combine(workspace,"server","world"));
        File.WriteAllText(Path.Combine(workspace,"server","world","level.dat"),"synthetic original world bytes");
        File.WriteAllText(Path.Combine(workspace,"java-path.txt"),fixture);
        File.WriteAllText(Path.Combine(workspace,"manager-settings.txt"),"4\n");
        File.WriteAllText(Path.Combine(workspace,"manager-profile.json"),new JavaScriptSerializer().Serialize(new ServerProfile{Name="Schedule fixture",MinecraftVersion="1.18.2"}));
        File.WriteAllText(Path.Combine(workspace,"connection-settings.json"),new JavaScriptSerializer().Serialize(new HostingOptions{Provider="direct",AutoVoice=false}));
        WriteScheduleFixtureOptions(workspace,false);
    }
    static bool WaitSchedulePhase(string workspace,string phase,int seconds)
    {
        DateTime end=DateTime.UtcNow.AddSeconds(seconds);
        while(DateTime.UtcNow<end){
            var state=ReadBootJson<ScheduleRunState>(ScheduleStatePath(workspace));
            if(state.Phase==phase)return true;
            if(state.Phase=="error")throw new Exception(state.Message);
            Thread.Sleep(100);
        }
        return false;
    }
    static int ScheduleChecks(string[] args)
    {
        string folder=BootArgument(args,"--workspace"),fixture=BootArgument(args,"--fixture");
        if(folder.Length==0||!File.Exists(fixture))return 2;
        string originalRoot=WorkspaceRoot,originalData=ManagerDataOverride;
        CraftHarbor ui=null;
        try{
            Directory.CreateDirectory(folder);ManagerDataOverride=Path.Combine(folder,"check-data");
            RunScheduleChecks();
            string workspace=Path.Combine(folder,"complete");PrepareScheduleFixture(workspace,fixture);WorkspaceRoot=workspace;
            ui=new CraftHarbor();ui.timer.Stop();
            ui.server=ui.LaunchPersistent("server",fixture,"-jar server.jar",ui.ServerDir,4);
            int firstPid=ui.server.Id;
            DateTime deadline=DateTime.UtcNow.AddSeconds(10);
            while(DateTime.UtcNow<deadline&&!ui.HostCall(new HostRequest()).Ready)Thread.Sleep(100);
            Require(ui.HostCall(new HostRequest()).Ready,"synthetic server reached ready");
            WriteScheduleFixtureOptions(workspace,true);
            // The host starts its own worker. The GUI is deliberately disposed before the due action.
            ui.Dispose();ui=null;
            Require(WaitSchedulePhase(workspace,"complete",125),"headless scheduled save/backup/restart completed");
            var state=ReadBootJson<ScheduleRunState>(ScheduleStatePath(workspace));
            Require(state.Claimed&&File.Exists(state.Backup),"completed occurrence and backup recorded durably");
            using(var zip=ZipFile.OpenRead(state.Backup)){
                // .NET Framework's Windows ZIP writer can retain backslashes. The restore path
                // already normalizes both separators; verify the actual world bytes, not its spelling.
                var world=zip.Entries.SingleOrDefault(e=>e.FullName.Replace('\\','/')=="world/level.dat");
                Require(world!=null,"full stopped-server backup contains original world");
                using(var reader=new StreamReader(world.Open()))Require(reader.ReadToEnd()=="synthetic original world bytes","stopped-server ZIP preserves exact original world content");
            }
            string[] commands=File.ReadAllLines(Path.Combine(workspace,"server","schedule-commands.txt"));
            int save=Array.IndexOf(commands,"save-all flush"),stop=Array.IndexOf(commands,"stop");
            Require(save>=0&&stop>save,"graceful save precedes stop before backup");
            Require(File.ReadAllLines(Path.Combine(workspace,"server","schedule-launches.txt")).Length==2,"exactly one scheduled restart");
            ui=new CraftHarbor();ui.timer.Stop();ui.AttachPersistentHost();
            Require(Running(ui.server)&&ui.server.Id!=firstPid&&ui.ready,"later GUI reattaches to restarted child");
            ui.HostCall(new HostRequest{Action="command",Text="stop"});ui.server.WaitForExit(5000);
            ui.HostCall(new HostRequest{Action="shutdown-idle"});ui.persistentAttached=false;ui.Dispose();ui=null;

            workspace=Path.Combine(folder,"manual-stop");PrepareScheduleFixture(workspace,fixture);WorkspaceRoot=workspace;
            ui=new CraftHarbor();ui.timer.Stop();ui.server=ui.LaunchPersistent("server",fixture,"-jar server.jar",ui.ServerDir,4);
            deadline=DateTime.UtcNow.AddSeconds(10);while(DateTime.UtcNow<deadline&&!ui.HostCall(new HostRequest()).Ready)Thread.Sleep(100);
            // Unknown and occupied replies cannot stop a server. Remove this file only after observing deferral.
            File.WriteAllText(Path.Combine(workspace,"server","players.txt"),"unknown");WriteScheduleFixtureOptions(workspace,true);
            Require(WaitSchedulePhase(workspace,"deferred",45),"missing participant response defers rather than stops");
            Require(File.ReadAllLines(Path.Combine(workspace,"server","schedule-launches.txt")).Length==1&&ui.HostCall(new HostRequest()).ServerPid>0,"unknown occupancy preserves original running child");
            File.WriteAllText(Path.Combine(workspace,"server","players.txt"),"0");
            Require(WaitSchedulePhase(workspace,"announced",45),"fresh empty reply permits countdown");
            long lastRunStart=ui.HostCall(new HostRequest()).ServerStart;
            ui.HostCall(new HostRequest{Action="command",Text="stop"});Require(ui.server.WaitForExit(5000),"manual graceful stop acknowledged during countdown");
            Require(lastRunStart>0&&ui.HostCall(new HostRequest()).ServerStart==lastRunStart,"stopped status retains the latest server run identity");
            Thread.Sleep(1500);
            Require(ui.HostCall(new HostRequest()).ServerPid==0,"manual stop is not reversed by schedule worker");
            Require(ReadBootJson<ScheduleRunState>(ScheduleStatePath(workspace)).Phase=="cancelled","manual stop has durable cancelled outcome");
            Require(File.ReadAllLines(Path.Combine(workspace,"server","schedule-launches.txt")).Length==1,"no duplicate launch or restart loop");
            ui.persistentAttached=false;Require(ui.CancelScheduledFromUi()&&ui.persistentAttached,"cancel probes a live host even when initial attachment was missed");
            ui.HostCall(new HostRequest{Action="shutdown-idle"});ui.persistentAttached=false;ui.Dispose();ui=null;
            File.WriteAllText(Path.Combine(folder,"schedule-verification.txt"),"PASS: deterministic local-time/daily-claim/delay-limit/unknown-occupancy policies; real singleton host with synthetic child survives GUI disposal, obtains fresh participant reply, announces, saves and gracefully stops, writes complete backup and restarts exactly once; reattachment; unknown occupancy deferral and manual stop during countdown cancel restart. No Minecraft, public tunnel, Windows task, credential, router or user PC was used. OS sleep and actual Minecraft saves remain unverified.");
            return 0;
        }catch(Exception error){File.WriteAllText(Path.Combine(folder,"schedule-verification.txt"),"FAIL: "+error);return 1;}
        finally{
            if(ui!=null){try{ui.HostCall(new HostRequest{Action="cancel-schedule"});if(Running(ui.server)){ui.HostCall(new HostRequest{Action="command",Text="stop"});ui.server.WaitForExit(5000);}ui.HostCall(new HostRequest{Action="shutdown-idle"});}catch{}ui.timer.Stop();ui.Dispose();}
            // A failure while the GUI was disposed must not orphan a synthetic child/host.
            foreach(string child in new[]{"complete","manual-stop"}){
                WorkspaceRoot=Path.Combine(folder,child);
                if(!ValidWorkspace(WorkspaceRoot))continue;
                CraftHarbor cleanup=null;
                try{cleanup=new CraftHarbor();cleanup.timer.Stop();cleanup.HostCall(new HostRequest{Action="cancel-schedule"});var status=cleanup.HostCall(new HostRequest());if(status.ServerPid>0){cleanup.HostCall(new HostRequest{Action="command",Text="stop"});var process=HostProcess(status.ServerPid,status.ServerStart);if(process!=null){process.WaitForExit(5000);process.Dispose();}}cleanup.HostCall(new HostRequest{Action="shutdown-idle"});}catch{}finally{if(cleanup!=null)cleanup.Dispose();}
            }
            WorkspaceRoot=originalRoot;ManagerDataOverride=originalData;
        }
    }
}
