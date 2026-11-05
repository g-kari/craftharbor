using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

partial class CraftHarbor
{
    static int FreeSettingsPort()
    {
        using(var socket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
            socket.Bind(new IPEndPoint(IPAddress.Loopback,0));return ((IPEndPoint)socket.LocalEndPoint).Port;
        }
    }
    static void WaitForStartupCheck(Func<bool> completed,string message)
    {
        DateTime deadline=DateTime.UtcNow.AddSeconds(10);
        while(!completed()&&DateTime.UtcNow<deadline){Application.DoEvents();Thread.Sleep(20);}
        Require(completed(),message);
    }
    static bool JavaProbeFixtureExited(Process child)
    {
        try{return child.HasExited;}catch(InvalidOperationException){return false;}catch(System.ComponentModel.Win32Exception){return false;}
    }
    static void CheckJavaProbeDiagnostics(string fixture)
    {
        string control=Path.Combine(Path.GetDirectoryName(fixture),"startup-fixture"),report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"java-verification.txt");
        byte[] originalReport=File.Exists(report)?File.ReadAllBytes(report):null;
        const string fields=@"state=(?:start-requested|started|wait-exited|wait-timeout|kill-attempt|kill-requested|kill-not-running|kill-failed|streams-read|final) elapsed_ms=\d+ started=(?:true|false) wait_exited=(?:true|false|not-waited) exited=(?:true|false|unknown|not-started) exit_code=(?:-?\d+|unknown) stderr=(?:not-started|pending|completed:\d+|faulted|cancelled) stdout=(?:not-started|pending|completed:\d+|faulted|cancelled)";
        Func<string,bool> safeReport=text=>text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Skip(1).All(line=>Regex.IsMatch(line,@"^probe=(?:runtime-properties|planned-flags) "+fields+"$"))&&!text.Contains("PRIVATE-FIXTURE-DO-NOT-LOG")&&!text.Contains(fixture);
        try{
            File.WriteAllText(control+".private-output","synthetic private property marker");
            Require(CheckInstalledJava(new[]{"--java-check",fixture})==0,"synthetic installed-Java diagnostics succeed");
            string text=File.ReadAllText(report);
            Require(text.StartsWith("PASS:")&&safeReport(text)&&text.Contains("probe=runtime-properties state=final")&&text.Contains("probe=planned-flags state=final"),"both successful diagnostic phases use only safe fixed fields");
            Require(text.Split('\n').Where(line=>line.Contains("state=final")).All(line=>line.Contains("wait_exited=true exited=true exit_code=0")&&line.Contains("stderr=completed:")&&line.Contains("stdout=completed:")),"successful probes record confirmed exits and completed stream lengths");
            File.WriteAllText(control+".fail","synthetic nonzero exit");
            Require(CheckInstalledJava(new[]{"--java-check",fixture})==1,"nonzero Java still fails validation");text=File.ReadAllText(report);
            Require(text.StartsWith("FAIL:")&&safeReport(text)&&text.Contains("wait_exited=true exited=true exit_code=1")&&!text.Contains("probe=planned-flags"),"failed metadata phase is identified without raw output or error text");
            File.Delete(control+".fail");
            Require(CheckInstalledJava(new[]{"--java-check",Path.Combine(Path.GetDirectoryName(fixture),"PRIVATE-FIXTURE-DO-NOT-LOG.exe")})==1,"missing executable still fails validation");text=File.ReadAllText(report);
            Require(text.StartsWith("FAIL:")&&safeReport(text)&&text.Contains("state=final")&&text.Contains("started=false wait_exited=not-waited exited=not-started exit_code=unknown"),"start failure does not include the executable path or arbitrary exception message");
            var timeout=new List<string>();bool rejected=false;
            try{RunJavaProbe(fixture,"--probe-timeout-fixture",state=>timeout.Add("probe=runtime-properties "+state));}catch(InvalidOperationException){rejected=true;}
            Require(rejected&&timeout.Any(line=>line.Contains("state=wait-timeout")&&line.Contains("started=true wait_exited=false"))&&timeout.Any(line=>line.Contains("state=kill-requested")||line.Contains("state=kill-not-running")),"unchanged eight-second probe timeout records its phase and kill request without claiming confirmed termination");
            Require(timeout.All(line=>Regex.IsMatch(line,@"^probe=runtime-properties "+fields+"$"))&&timeout.Last().Contains("state=final"),"timeout diagnostics contain only fixed fields and a final observation");
        }finally{
            try{
                if(File.Exists(control+".timeout-pid")){
                    int pid=int.Parse(File.ReadAllText(control+".timeout-pid"),System.Globalization.CultureInfo.InvariantCulture);Process child=null;
                    try{child=Process.GetProcessById(pid);}catch(ArgumentException){}
                    if(child!=null)using(child){
                        try{
                            if(!JavaProbeFixtureExited(child)){
                                var module=child.MainModule;
                                if(!JavaProbeFixtureExited(child)){Require(module!=null&&string.Equals(module.FileName,fixture,StringComparison.OrdinalIgnoreCase),"timeout cleanup targets only its own synthetic executable");child.Kill();Require(child.WaitForExit(5000),"synthetic timeout process cleanup completes");}
                            }
                        }catch(InvalidOperationException){Require(JavaProbeFixtureExited(child),"timeout cleanup confirms an exit after a process-state race");}catch(System.ComponentModel.Win32Exception){Require(JavaProbeFixtureExited(child),"timeout cleanup confirms an exit after a module-access race");}
                    }
                }
            }finally{
                foreach(string suffix in new[]{".private-output",".fail",".results",".timeout-pid"})if(File.Exists(control+suffix))File.Delete(control+suffix);
                if(originalReport==null)File.Delete(report);else File.WriteAllBytes(report,originalReport);
            }
        }
    }
    void CheckStartupCancellation(string fixture)
    {
        string control=Path.Combine(Path.GetDirectoryName(fixture),"startup-fixture"),javaFile=Path.Combine(root,"java-path.txt");
        byte[] originalJava=File.Exists(javaFile)?File.ReadAllBytes(javaFile):null;
        bool automatic=autoMemory.Checked;decimal originalMemory=memory.Value;var originalHosting=hosting;
        Task startup=null;
        Func<int> launches=()=>{try{return File.Exists(control+".launches")?File.ReadAllLines(control+".launches").Length:0;}catch(IOException){return -1;}};
        try{
            File.WriteAllText(javaFile,fixture);autoMemory.Checked=false;memory.Value=4;
            hosting=new HostingOptions{Provider="direct",AutoVoice=false};
            workspace.SelectedIndex=5;Show();Application.DoEvents();
            var allStop=Descendants(this).OfType<Button>().Single(button=>button.Text=="全部停止");
            Require(allStop.Visible&&allStop.Enabled,"all-stop control is available during startup preparation");
            int beforeTunnel=launches();tunnel=LaunchPersistent("tunnel",fixture,"",ServerDir,0);
            WaitForStartupCheck(()=>Running(tunnel)&&launches()==beforeTunnel+1,"synthetic tunnel records its launch before the cancellation baseline");
            int before=launches();
            foreach(bool fail in new[]{false,true}){
                int cancellations=queue.ToArray().Count(line=>line=="[管理] 起動準備を中止しました。サーバーは起動していません。"),errors=queue.ToArray().Count(line=>line.StartsWith("[起動エラー]"));
                foreach(string suffix in new[]{".started",".release",".fail",".probes",".results"})if(File.Exists(control+suffix))File.Delete(control+suffix);
                File.WriteAllText(control+".hold","synthetic Java preflight gate");if(fail)File.WriteAllText(control+".fail","synthetic failure");
                startup=StartServerAsync();
                WaitForStartupCheck(()=>File.Exists(control+".started")||startup.IsCompleted,"synthetic Java probe reaches held preflight");
                Require(File.Exists(control+".started")&&preparingServer&&!startup.IsCompleted,"real startup path is awaiting Java preflight");
                allStop.PerformClick();allStop.PerformClick();
                Require(cancelServerStart&&preparingServer&&!Running(tunnel),"repeated all-stop cancels startup and stops the synthetic tunnel without releasing the preparation guard");
                Require(StartServerAsync().IsCompleted&&cancelServerStart&&preparingServer,"duplicate start cannot reset cancellation while the old probe is pending");
                RequestRestart();Require(cancelServerStart&&preparingServer&&!restartPending&&File.ReadAllLines(control+".probes").Length==1,"restart during cancelled preparation cannot queue or launch a second probe");
                File.WriteAllText(control+".release","release");
                WaitForStartupCheck(()=>startup.IsCompleted,"cancelled startup completes after the probe is released");startup.GetAwaiter().GetResult();
                Require(File.ReadAllLines(control+".results").SequenceEqual(fail?new[]{"deliberate-failure"}:new[]{"settings-success","flags-success"}),"cancelled probe outcome is the intended success or failure, never a gate timeout");
                Require(!preparingServer&&!Running(server)&&!Running(tunnel)&&launches()==before,"no server launch follows all-stop, even when the cancelled probe fails");
                Require(detail.Text.Contains("起動準備を中止しました"),"cancelled preflight is reported as cancellation rather than a startup failure");
                Require(queue.ToArray().Count(line=>line=="[管理] 起動準備を中止しました。サーバーは起動していません。") == cancellations+1&&queue.ToArray().Count(line=>line.StartsWith("[起動エラー]"))==errors,"one cancellation notice and no misleading startup error");
                RefreshDashboardPresentation(false,false);Require(start.Enabled&&!backupBusy,"a cancelled preparation returns to an available start control");
            }
            foreach(string suffix in new[]{".hold",".started",".release"})if(File.Exists(control+suffix))File.Delete(control+suffix);
            startup=StartServerAsync();WaitForStartupCheck(()=>startup.IsCompleted,"synthetic preflight failure completes");startup.GetAwaiter().GetResult();
            Require(!cancelServerStart&&!preparingServer&&!Running(server)&&detail.Text.StartsWith("起動できませんでした"),"a fresh failed attempt is not hidden by an earlier cancellation");
            File.Delete(control+".fail");
            startup=StartServerAsync();WaitForStartupCheck(()=>startup.IsCompleted,"fresh synthetic startup completes");startup.GetAwaiter().GetResult();
            WaitForStartupCheck(()=>{PollPersistentHost(true);return ready&&Running(server);},"fresh explicit start reaches ready after cancellation");
            Require(!cancelServerStart&&launches()==before+1,"fresh start clears the old cancellation and launches once");
            StartServerAsync().GetAwaiter().GetResult();Require(launches()==before+1,"repeated start while running does not launch twice");
            RequestRestart();Require(restartPending&&!cancelServerStart,"an intentional restart is not cancelled by its graceful-stop step");
            Require(server.WaitForExit(5000),"restart stops the first synthetic server gracefully");PollPersistentHost(true);
            previousRunning=true;UpdateFeatures(false,false);
            WaitForStartupCheck(()=>{PollPersistentHost(true);return !preparingServer&&ready&&Running(server)&&launches()==before+2;},"intentional restart launches the next synthetic server");
            Require(!restartPending&&launches()==before+2,"intentional restart is consumed once");
            allStop.PerformClick();Require(server.WaitForExit(5000),"all-stop still gracefully stops an already running synthetic server");PollPersistentHost(true);
            Require(!Running(server)&&!Running(tunnel)&&!restartPending,"all-stop leaves synthetic processes stopped without a queued restart");
        }finally{
            if(preparingServer)cancelServerStart=true;
            File.WriteAllText(control+".release","release during cleanup");
            DateTime deadline=DateTime.UtcNow.AddSeconds(10);
            while((preparingServer||startup!=null&&!startup.IsCompleted)&&DateTime.UtcNow<deadline){Application.DoEvents();Thread.Sleep(20);}
            try{if(Running(server)){HostCall(new HostRequest{Action="command",Text="stop"});server.WaitForExit(5000);}if(Running(tunnel))HostCall(new HostRequest{Action="stop-tunnel"});}catch{}
            foreach(string suffix in new[]{".hold",".started",".release",".fail",".probes",".results"})if(File.Exists(control+suffix))File.Delete(control+suffix);
            if(originalJava==null)File.Delete(javaFile);else File.WriteAllBytes(javaFile,originalJava);
            hosting=originalHosting;memory.Value=originalMemory;autoMemory.Checked=automatic;Hide();
        }
    }
    static int SettingsChecks(string[] args)
    {
        if(args.Length!=3)return 2;
        WorkspaceRoot=Path.GetFullPath(args[1]);ManagerDataOverride=Path.Combine(WorkspaceRoot,"check-data");
        string directory=Path.Combine(WorkspaceRoot,"server"),report=Path.Combine(WorkspaceRoot,"settings-verification.txt");
        CraftHarbor ui=null;
        try {
            Directory.CreateDirectory(directory);
            int oldPort=FreeSettingsPort(),nextPort=FreeSettingsPort();while(nextPort==oldPort)nextPort=FreeSettingsPort();
            string properties=Path.Combine(directory,"server.properties");
            string original="# keep\nserver-port="+oldPort+"\nmotd=Original\nview-distance=8\nsimulation-distance=6\nonline-mode=true\ncustom=keep\n";
            File.WriteAllText(properties,original);File.WriteAllText(Path.Combine(directory,"server.jar"),"synthetic");
            foreach(string file in new[]{"whitelist.json","ops.json","banned-players.json"})File.WriteAllText(Path.Combine(directory,file),"[]");
            File.WriteAllText(Path.Combine(WorkspaceRoot,"manager-profile.json"),new JavaScriptSerializer().Serialize(new ServerProfile{Name="Current",MinecraftVersion="1.18.2"}));
            ui=new CraftHarbor();ui.timer.Stop();
            ui.server=ui.LaunchPersistent("server",Path.GetFullPath(args[2]),"",directory,33);
            DateTime end=DateTime.UtcNow.AddSeconds(10);
            while(!ui.ready&&DateTime.UtcNow<end){Thread.Sleep(100);ui.PollPersistentHost(true);}
            Require(ui.ready,"synthetic server ready");
            int pid=ui.server.Id;
            ui.autoDistances.Checked=false;ui.viewSetting.Value=14;ui.simulationSetting.Value=10;
            ui.motdSetting.Text="Next $' value";ui.ApplyServerSettings();
            ui.profileName.Text="Next profile";ui.portSetting.Value=nextPort;ui.SaveProfile();
            Require(File.ReadAllText(properties)==original,"saving during runtime never writes server.properties");
            Require(ui.profile.Name=="Current"&&ui.ServerPort==oldPort,"runtime identity and tunnel port remain current");
            Require(ui.DistancePreviewHeap()==33,"running preview retains allocated heap rather than subtracting its own RAM");
            Require(ui.pendingSettingsStatus.Text.Contains("再起動待ち"),"pending restart status shown");
            Require(ui.server.Id==pid&&Running(ui.server),"settings save does not restart or stop server");
            ui.Text="Synthetic pending settings check";ui.Show();System.Windows.Forms.Application.DoEvents();
            ui.SetReviewSize(new System.Drawing.Size(1024,720));ui.workspace.SelectedIndex=3;ui.SelectHarborPage();ui.TickState();
            ui.CaptureHarborPage("ui-pending-settings.png",3);
            Require(ui.dashboardTitle.Text=="サーバー設定"&&ui.pendingSettingsStatus.Width>400&&ui.profileName.Visible,"pending settings capture has completed layout");
            ui.Hide();
            ui.Dispose();ui=new CraftHarbor();ui.timer.Stop();
            Require(ui.server!=null&&ui.server.Id==pid,"settings reopen reattaches existing process");
            Require(ui.motdSetting.Text=="Next $' value"&&ui.profileName.Text=="Next profile"&&ui.portSetting.Value==nextPort,"pending editors survive reopening");
            Require(ui.profile.Name=="Current"&&ui.ServerPort==oldPort,"pending profile is separate from current profile after reopen");
            bool blocked=false;try{ui.EnsureStopped();}catch(Exception){blocked=true;}Require(blocked,"stopped-only whitelist file update is guarded while running");
            ui.HostCall(new HostRequest{Action="command",Text="stop"});Require(ui.server.WaitForExit(5000),"fixture stopped");
            // Simulate a server writing its old properties during shutdown.
            File.WriteAllText(properties,original);
            using(var socket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
                socket.ExclusiveAddressUse=true;socket.Bind(new IPEndPoint(IPAddress.Loopback,nextPort));socket.Listen(1);
                blocked=false;try{using(var maintenance=WorkspaceMaintenanceLock(WorkspaceRoot))ApplyPendingServerSettings(WorkspaceRoot);}catch(SocketException){blocked=true;}
                Require(blocked&&File.Exists(PendingSettingsPath(WorkspaceRoot))&&File.ReadAllText(properties)==original,"occupied new port retains draft without partial write");
            }
            ui.PrepareSavedSettingsForStart();
            string applied=File.ReadAllText(properties);
            Require(applied.Contains("motd=Next $' value")&&applied.Contains("simulation-distance=10")&&applied.Contains("server-port="+nextPort),"pending changes applied only before launch");
            Require(applied.Contains("online-mode=true")&&applied.Contains("custom=keep")&&applied.Contains("# keep"),"unrelated authentication and server properties preserved");
            Require(!File.Exists(PendingSettingsPath(WorkspaceRoot))&&ui.profile.Name=="Next profile","draft consumed and active profile loaded");
            Require(File.ReadAllText(properties+".before-settings.bak")==original,"pre-change properties backed up");
            Require(!HasPendingSettings(ReadPendingSettings(Path.Combine(WorkspaceRoot,"other-profile"))),"draft isolated by profile");
            blocked=false;try{PatchServerProperties(original,new Dictionary<string,string>{{"online-mode","false"}});}catch(InvalidDataException){blocked=true;}Require(blocked,"draft cannot modify authentication outside editor scope");
            var identity=ParseWhitelistIdentity("{\"id\":\"123456781234423492341234567890ab\",\"name\":\"ExampleUser\"}","exampleuser");
            EditStoppedWhitelist(directory,"add","ExampleUser",identity);
            EditStoppedWhitelist(directory,"add","exampleuser",identity);
            var rows=new JavaScriptSerializer().Deserialize<List<Dictionary<string,object>>>(File.ReadAllText(Path.Combine(directory,"whitelist.json")));
            Require(rows.Count==1&&StringValue(rows[0],"uuid")=="12345678-1234-4234-9234-1234567890ab","offline whitelist uses verified UUID and deduplicates names");
            EditStoppedWhitelist(directory,"remove","EXAMPLEUSER",null);
            Require(File.ReadAllText(Path.Combine(directory,"whitelist.json"))=="[]","stopped whitelist removal does not require network lookup");
            blocked=false;try{ParseWhitelistIdentity("{\"id\":\"123456781234423492341234567890ab\",\"name\":\"Different\"}","ExampleUser");}catch(InvalidDataException){blocked=true;}Require(blocked,"mismatched identity rejected");
            blocked=false;try{ValidatePlayerName("name\nstop");}catch(ArgumentException){blocked=true;}Require(blocked,"name command injection rejected");
            Require(OfflineIdentity("Notch").Uuid=="b50ad385-829d-3141-a216-7e7d7539ba7f","explicit offline-mode UUID algorithm");
            File.WriteAllText(Path.Combine(directory,"whitelist.json"),"{broken");
            blocked=false;try{EditStoppedWhitelist(directory,"remove","ExampleUser",null);}catch{blocked=true;}
            Require(blocked&&File.ReadAllText(Path.Combine(directory,"whitelist.json"))=="{broken","malformed whitelist preserved");
            File.WriteAllText(PendingSettingsPath(WorkspaceRoot),"{broken");ui.LoadServerSettings();
            Require(ui.EditingProfile().Name=="Next profile","damaged draft does not prevent viewing current profile");
            blocked=false;try{StageServerSettings(WorkspaceRoot,delegate(PendingServerSettings draft){draft.Properties["motd"]="replace";});}catch{blocked=true;}
            Require(blocked&&File.ReadAllText(PendingSettingsPath(WorkspaceRoot))=="{broken","damaged draft is not silently overwritten");
            File.Delete(PendingSettingsPath(WorkspaceRoot));CheckDistancePolicy();
            ui.CheckStartupCancellation(Path.GetFullPath(args[2]));
            CheckJavaProbeDiagnostics(Path.GetFullPath(args[2]));
            ui.HostCall(new HostRequest{Action="shutdown-idle"});
            File.WriteAllText(report,"PASS: runtime settings/profile save without restart or active-port change; durable next-start draft; UI reopening; occupied-port rejection; shutdown rewrite preservation; atomic next-start apply and backup; unrelated properties preserved; per-profile isolation; whitelist add/remove/UUID validation/deduplication/corrupt-file protection; manual and five-tier distance policies and running heap preview; all-stop during held successful/failed Java preflight, repeated stop/start, fresh retry, graceful stop and intentional restart; sanitized Java probe phase/exit/stream diagnostics for success, nonzero exit, start failure and unchanged eight-second timeout. Synthetic processes only; no Minecraft server, real account lookup, external tunnel or PC restart.");
            return 0;
        }catch(Exception error){File.WriteAllText(report,"FAIL: "+error);return 1;}
        finally {
            if(ui!=null){
                try{ui.HostCall(new HostRequest{Action="command",Text="stop"});if(ui.server!=null)ui.server.WaitForExit(5000);}catch{}
                try{ui.HostCall(new HostRequest{Action="shutdown-idle"});}catch{}
                ui.timer.Stop();ui.Dispose();
            }
        }
    }
}
