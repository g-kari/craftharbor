using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Web.Script.Serialization;
using System.Xml;

partial class CraftHarbor
{
    static int BootChecks(string[] args)
    {
        string workspace=BootArgument(args,"--workspace"), fixture=BootArgument(args,"--fixture");
        if(workspace.Length==0||!File.Exists(fixture))return 2;
        WorkspaceRoot=CanonicalWorkspace(workspace);
        string report=Path.Combine(workspace,"boot-verification.txt");
        CraftHarbor ui=null;Process host=null;dynamic service=null,definition=null;
        try {
            string directory=Path.Combine(workspace,"server");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"server.jar"),"synthetic fixture");
            int port;
            using(var socket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){
                socket.Bind(new IPEndPoint(IPAddress.Loopback,0));port=((IPEndPoint)socket.LocalEndPoint).Port;
            }
            File.WriteAllText(Path.Combine(directory,"server.properties"),"server-port="+port+"\nview-distance=8\nsimulation-distance=6\nmotd=keep-me\n");
            File.WriteAllText(Path.Combine(directory,"eula.txt"),"eula=true\n");
            File.WriteAllText(Path.Combine(workspace,"java-path.txt"),fixture);
            File.WriteAllText(Path.Combine(workspace,"manager-options.json"),"{\"autoMemory\":false}");
            File.WriteAllText(Path.Combine(workspace,"manager-settings.txt"),"4\n");
            File.WriteAllText(Path.Combine(workspace,"manager-profile.json"),new JavaScriptSerializer().Serialize(new ServerProfile{Name="Boot fixture",MinecraftVersion="1.18.2"}));
            File.WriteAllText(Path.Combine(workspace,"connection-settings.json"),new JavaScriptSerializer().Serialize(new HostingOptions{Provider="ngrok",NgrokExe=fixture}));
            SaveBootOptions(workspace,new BootOptions{Enabled=true,Connect=true,OwnerSid=CurrentSid(),DelaySeconds=30});
            Require(!BootOwnerMatches("S-1-0-0")&&BootOwnerMatches(CurrentSid()),"owner SID binding");
            Require(BootTaskName(workspace,CurrentSid())!=BootTaskName(workspace+"-other",CurrentSid()),"per-profile task isolation");
            Require(MemoryGateName().StartsWith("Global\\"),"memory reservations cross sessions");
            Require(BackgroundTunnelRequest(workspace,new HostingOptions{Provider="direct"},port)==null,"direct provider does not spawn a tunnel");
            bool blocked=false;try{BackgroundTunnelRequest(workspace,new HostingOptions{Provider="port-tool"},port);}catch(InvalidOperationException){blocked=true;}
            Require(blocked,"interactive port tool rejected");
            using(var socket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)) {
                socket.ExclusiveAddressUse=true;socket.Bind(new IPEndPoint(IPAddress.Loopback,port));socket.Listen(1);
                blocked=false;try{CheckBootPort(directory);}catch(SocketException){blocked=true;}Require(blocked,"occupied port is not reused");
            }
            service=OpenTaskService();
            definition=MakeBootDefinition(service,BootHostExecutable,workspace,CurrentSid(),30);
            var xml=new XmlDocument();xml.LoadXml((string)definition.XmlText);
            var ns=new XmlNamespaceManager(xml.NameTable);ns.AddNamespace("t","http://schemas.microsoft.com/windows/2004/02/mit/task");
            Require(xml.SelectSingleNode("//t:BootTrigger/t:Delay",ns).InnerText=="PT30S","real Task Scheduler boot trigger");
            Require(xml.SelectSingleNode("//t:Principal/t:RunLevel",ns).InnerText=="LeastPrivilege","task never elevated");
            Require(xml.SelectSingleNode("//t:Principal/t:LogonType",ns).InnerText=="Password","batch logon for pre-sign-in network and DPAPI");
            Require(xml.SelectSingleNode("//t:MultipleInstancesPolicy",ns).InnerText=="IgnoreNew","duplicate task starts ignored");
            Require(xml.SelectSingleNode("//t:ExecutionTimeLimit",ns).InnerText=="PT0S","no default 72-hour server cutoff");
            Require(xml.SelectSingleNode("//t:Exec/t:Arguments",ns).InnerText.Contains(Q(workspace)),"absolute workspace passed explicitly");
            Require(xml.SelectSingleNode("//t:RegistrationTrigger",ns)==null,"registration cannot start server");
            File.WriteAllText(Path.Combine(workspace,"boot-task-definition.xml"),(string)definition.XmlText);
            host=Process.Start(new ProcessStartInfo(BootHostExecutable,"--boot-start --workspace "+Q(workspace)+" --owner-sid "+Q(CurrentSid())){UseShellExecute=false,CreateNoWindow=true});
            ui=new CraftHarbor();ui.timer.Stop();
            HostReply reply=null;DateTime deadline=DateTime.UtcNow.AddSeconds(45);
            while(DateTime.UtcNow<deadline){
                try{reply=ui.HostCall(new HostRequest(),200);if(reply.Ready&&reply.TunnelPid>0&&!reply.BootInitializing)break;}catch(TimeoutException){}catch(IOException){}
                Thread.Sleep(200);
            }
            Require(reply!=null&&reply.Ready&&reply.TunnelPid>0&&!reply.BootInitializing,"headless boot starts server and configured public connection");
            int serverPid=reply.ServerPid,tunnelPid=reply.TunnelPid;
            Require(File.ReadAllText(Path.Combine(directory,"server.properties")).Contains("motd=keep-me"),"unrelated world/server properties preserved");
            using(var duplicate=Process.Start(new ProcessStartInfo(BootHostExecutable,"--workspace "+Q(workspace)){UseShellExecute=false,CreateNoWindow=true})) {
                Require(duplicate.WaitForExit(5000)&&duplicate.ExitCode==0,"duplicate host exits without replacing processes");
            }
            ui.Dispose();ui=new CraftHarbor();ui.timer.Stop();ui.AttachPersistentHost();
            Require(ui.server!=null&&ui.server.Id==serverPid&&ui.tunnel!=null&&ui.tunnel.Id==tunnelPid,"later UI reconnects to same boot processes");
            ui.HostCall(new HostRequest{Action="command",Text="ping"});Thread.Sleep(200);
            Require(ui.HostCall(new HostRequest()).Lines.Any(l=>l.Text.Contains("BOOT_CHECK_PONG")),"console IPC works after reconnect");
            ui.HostCall(new HostRequest{Action="stop-tunnel"});
            Require(ui.HostCall(new HostRequest()).ServerPid==serverPid,"public stop is independent of server");
            ui.HostCall(new HostRequest{Action="command",Text="stop"});
            deadline=DateTime.UtcNow.AddSeconds(5);while(DateTime.UtcNow<deadline&&ui.HostCall(new HostRequest()).ServerPid!=0)Thread.Sleep(100);
            Thread.Sleep(600);
            Require(ui.HostCall(new HostRequest()).ServerPid==0,"manual stop retained");
            Require(File.ReadAllLines(Path.Combine(directory,"boot-launches.txt")).Length==1,"one launch only");
            ui.HostCall(new HostRequest{Action="shutdown-idle"});Require(host.WaitForExit(5000),"idle host exits");
            // Exercise retry cancellation without waiting through or changing production retry delays.
            bootInitializing=true;bootCancelled=false;
            var retry=System.Threading.Tasks.Task.Run(delegate {
                File.WriteAllText(Path.Combine(directory,"eula.txt"),"eula=false\n");RunBootRecovery(workspace);
            });
            deadline=DateTime.UtcNow.AddSeconds(10);
            while(DateTime.UtcNow<deadline) {
                try{if(ReadBootJson<BootReport>(Path.Combine(workspace,"boot-status.json")).Phase=="error")break;}catch(IOException){}
                Thread.Sleep(100);
            }
            ExecuteHost(new HostRequest{Action="cancel-boot"},workspace);
            Require(retry.Wait(5000)&&!bootInitializing,"manual cancellation interrupts pending startup retry");
            Require(File.ReadAllLines(Path.Combine(directory,"boot-launches.txt")).Length==1,"failed EULA never launches process");
            File.WriteAllText(report,"PASS: Windows Task Scheduler COM definition (boot trigger, least privilege, batch logon, no execution timeout, no registration trigger); profile/SID isolation; port collision; headless fake server and configured tunnel; one host; UI reconnect retaining both PIDs; independent tunnel stop; graceful server stop; pending retry cancellation; EULA and server settings preservation. No real Windows task registered, password requested, public tunnel created or machine rebooted. Cross-session interactive logon and actual reboot remain manual validation.");
            return 0;
        }catch(Exception error){File.WriteAllText(report,"FAIL: "+error);return 1;}
        finally {
            bootCancelled=true;
            if(ui!=null){
                try{ui.HostCall(new HostRequest{Action="cancel-boot"});}catch{}
                try{ui.HostCall(new HostRequest{Action="stop-tunnel"});}catch{}
                try{ui.HostCall(new HostRequest{Action="command",Text="stop"});}catch{}
                try{ui.HostCall(new HostRequest{Action="shutdown-idle"});}catch{}
                ui.timer.Stop();ui.Dispose();
            }
            if(host!=null){if(!host.WaitForExit(5000))host.Kill();host.Dispose();}
            ReleaseTaskObject(definition);ReleaseTaskObject(service);
        }
    }
}
