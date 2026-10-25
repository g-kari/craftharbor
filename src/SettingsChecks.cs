using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Web.Script.Serialization;

partial class CraftHarbor
{
    static int FreeSettingsPort()
    {
        using(var socket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
            socket.Bind(new IPEndPoint(IPAddress.Loopback,0));return ((IPEndPoint)socket.LocalEndPoint).Port;
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
            ui.Size=new System.Drawing.Size(1024,720);ui.Text="Synthetic pending settings check";
            ui.workspace.SelectedIndex=3;ui.Show();System.Windows.Forms.Application.DoEvents();
            using(var image=new System.Drawing.Bitmap(ui.Width,ui.Height)){ui.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(WorkspaceRoot,"ui-pending-settings.png"),System.Drawing.Imaging.ImageFormat.Png);}
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
            ui.HostCall(new HostRequest{Action="shutdown-idle"});
            File.WriteAllText(report,"PASS: runtime settings/profile save without restart or active-port change; durable next-start draft; UI reopening; occupied-port rejection; shutdown rewrite preservation; atomic next-start apply and backup; unrelated properties preserved; per-profile isolation; whitelist add/remove/UUID validation/deduplication/corrupt-file protection; manual and five-tier distance policies and running heap preview. Synthetic processes only; no Minecraft server, real account lookup, external tunnel or PC restart.");
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
