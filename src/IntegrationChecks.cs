using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

partial class CraftHarbor
{
    static int PackDownloadCheck(string[] args)
    {
        if(args.Length!=2)return 2;string destination=Path.GetFullPath(args[1]);Directory.CreateDirectory(destination);try{var preset=PackPresets().First();string zip=Path.Combine(destination,"download.zip"),unpacked=Path.Combine(destination,"unpacked");DownloadPackFile(preset.Url,zip,preset.Sha256,CancellationToken.None,p=>{});ExtractPack(zip,unpacked,CancellationToken.None,p=>{});string root=FindPackRoot(unpacked);foreach(var extra in preset.Extras)DownloadPackFile(extra.Url,Path.Combine(destination,"extra",extra.Path),extra.Sha256,CancellationToken.None,p=>{});Require(File.Exists(Path.Combine(root,"server.jar")),"official server launch file");File.WriteAllText(Path.Combine(destination,"download-verification.txt"),"PASS: official HTTPS server ZIP downloaded, SHA-256 verified and safely extracted; pinned additional MOD downloaded and SHA-256 verified. No EULA file was created or modified, no workspace was registered and no downloaded code was executed.");return 0;}catch(Exception e){File.WriteAllText(Path.Combine(destination,"download-verification.txt"),"FAIL: "+e);return 1;}
    }
    static int HostCheck(string[] args)
    {
        if(args.Length!=3)return 2;WorkspaceRoot=Path.GetFullPath(args[1]);ManagerDataOverride=Path.Combine(WorkspaceRoot,"check-data");CraftHarbor first=null,second=null;
        try{
            first=new CraftHarbor();first.server=first.LaunchPersistent("server",Path.GetFullPath(args[2]),"",first.ServerDir,4);int pid=first.server.Id;
            DateTime end=DateTime.UtcNow.AddSeconds(10);while(!first.ready&&DateTime.UtcNow<end){Thread.Sleep(100);first.PollPersistentHost(true);}Require(first.ready,"fixture reached ready");first.timer.Stop();first.Dispose();first=null;
            second=new CraftHarbor();second.timer.Stop();Require(Running(second.server)&&second.server.Id==pid&&second.ready,"same child survives UI disposal and reattachment");
            second.Show();second.workspace.SelectedIndex=1;System.Windows.Forms.Application.DoEvents();
            second.stopping=true;
            foreach(bool enter in new[]{false,true}){
                second.CheckRejectedConsoleDraft("  /ping  ",enter,"stopping");second.CheckRejectedConsoleDraft("  /ping  ",enter,"repeated stopping");
            }
            second.PollPersistentHost(true);Require(!second.queue.ToArray().Any(line=>line.Contains("HOST_CHECK_PONG")),"stopping rejection sends no ping to the synthetic host");second.stopping=false;
            foreach(bool enter in new[]{false,true}){
                int replies=second.queue.ToArray().Count(line=>line.Contains("HOST_CHECK_PONG")),echoes=second.queue.ToArray().Count(line=>line=="> ping");
                second.command.Text="  /ping  ";second.SubmitConsoleCommandForCheck(enter);
                Require(second.command.Text==""&&second.queue.ToArray().Count(line=>line=="> ping")==echoes+1,"accepted console submission clears the draft and echoes one normalized command");
                DateTime replyDeadline=DateTime.UtcNow.AddSeconds(5);
                while(second.queue.ToArray().Count(line=>line.Contains("HOST_CHECK_PONG"))==replies&&DateTime.UtcNow<replyDeadline){Thread.Sleep(50);second.PollPersistentHost(true);}
                Require(second.queue.ToArray().Count(line=>line.Contains("HOST_CHECK_PONG"))==replies+1,"console command reaches the reattached synthetic server once");
            }
            string oversized=new string('x',32769);second.command.Text=oversized;int sentBefore=second.queue.ToArray().Count(line=>line.StartsWith("> ",StringComparison.Ordinal));bool rejected=false;
            try{second.SendCommand();}catch(InvalidOperationException){rejected=true;}
            Require(rejected&&second.command.Text==oversized&&second.queue.ToArray().Count(line=>line.StartsWith("> ",StringComparison.Ordinal))==sentBefore,"host rejection preserves the draft without a successful echo");
            second.command.Clear();second.Hide();second.SendQuiet("spark tps");DateTime metricsEnd=DateTime.UtcNow.AddSeconds(5);while((double.IsNaN(second.tps)||double.IsNaN(second.mspt))&&DateTime.UtcNow<metricsEnd){Thread.Sleep(100);second.PollPersistentHost(true);}Require(second.tps==20.0&&second.mspt==0.8,"quiet metric command and reply after UI reattachment");second.command.Text="stop";second.SendCommand();Require(second.command.Text==""&&second.stopping,"explicit console stop retains graceful-stop handling and clears the input");Require(second.server.WaitForExit(5000),"fixture exited");second.PollPersistentHost(true);Require(second.serverExitCode==0,"graceful stop after UI reconnection");second.HostCall(new HostRequest{Action="shutdown-idle"});second.persistentAttached=false;
            File.WriteAllText(Path.Combine(WorkspaceRoot,"host-verification.txt"),"PASS: isolated fake server retained the same PID while the UI was disposed; a second UI reattached, received logs, preserved rejected console drafts through button and Enter while stopping, cleared only host-accepted commands verified by separate ping replies, preserved a host-rejected oversized draft, sent a simulated spark query, parsed TPS/MSPT and stopped the child gracefully. No Minecraft server, public tunnel or Discord message was used.");return 0;
        }catch(Exception e){File.WriteAllText(Path.Combine(WorkspaceRoot,"host-verification.txt"),"FAIL: "+e);return 1;}
        finally{foreach(var app in new[]{first,second})if(app!=null){try{if(Running(app.server)){app.HostCall(new HostRequest{Action="command",Text="stop"});app.server.WaitForExit(5000);}app.HostCall(new HostRequest{Action="shutdown-idle"});}catch(Exception){}if(app.timer!=null)app.timer.Stop();app.Dispose();}}
    }
    static void CheckPackLaunchCase(string scratch,string scenario,string[] members,string expectedMode,string expectedTarget)
    {
        string fixture=Path.Combine(scratch,"launch-"+scenario);Directory.CreateDirectory(fixture);
        string archivePath=Path.Combine(fixture,"pack.zip"),unpacked=Path.Combine(fixture,"unpacked"),workspace=Path.Combine(fixture,"workspace");
        using(var archive=ZipFile.Open(archivePath,ZipArchiveMode.Create))foreach(string member in members)
            using(var writer=new StreamWriter(archive.CreateEntry("pack/"+member).Open()))writer.Write("synthetic launch fixture; never executed");
        string archiveHash=Sha256File(archivePath);
        ExtractPack(archivePath,unpacked,CancellationToken.None,p=>{});Directory.CreateDirectory(workspace);
        bool rejected=false;
        try{PreparePack(unpacked,workspace,"Synthetic "+scenario,25569,null,"");}
        catch(InvalidDataException error){if(!error.Message.Contains("一意に判別"))throw;rejected=true;}
        string profilePath=Path.Combine(workspace,"manager-profile.json");
        if(expectedMode==null){
            Require(rejected,scenario+" rejects ambiguous or missing launch targets");
            Require(!File.Exists(profilePath),scenario+" does not publish a guessed launch profile");
        }else{
            Require(!rejected&&File.Exists(profilePath),scenario+" writes a launch profile");
            var imported=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<ServerProfile>(File.ReadAllText(profilePath));
            Require(imported.LaunchMode==expectedMode,scenario+" retains the expected launch mode");
            string target=expectedMode=="argfile"?imported.ArgumentsFile:imported.JarFile;
            Require(target.Replace('\\','/')==expectedTarget,scenario+" selects the exact expected relative path");
            Require(ValidWorkspace(workspace),scenario+" remains a valid workspace");
        }
        Require(Sha256File(archivePath)==archiveHash,scenario+" leaves the source archive unchanged");
    }
    static void CheckPackLaunchSelection(string scratch)
    {
        const string first="libraries/net/minecraftforge/forge/1.20.1-47.2.0/win_args.txt";
        const string second="libraries/net/minecraftforge/forge/1.20.1-47.3.0/win_args.txt";
        CheckPackLaunchCase(scratch,"ambiguous-argfiles",new[]{first,second},null,null);
        CheckPackLaunchCase(scratch,"reversed-argfiles",new[]{second,first},null,null);
        CheckPackLaunchCase(scratch,"single-argfile",new[]{second},"argfile",second);
        CheckPackLaunchCase(scratch,"unrelated-argfiles",new[]{second,"libraries/old/unix_args.txt","libraries/old/win_args.txt.bak","libraries/README.txt","other/win_args.txt","run.bat"},"argfile",second);
        CheckPackLaunchCase(scratch,"missing-argfile",new[]{"libraries/README.txt","libraries/old/unix_args.txt"},null,null);
        CheckPackLaunchCase(scratch,"server-jar-priority",new[]{"server.jar","fabric-server-launch.jar",first,second},"jar","server.jar");
        CheckPackLaunchCase(scratch,"fabric-jar-priority",new[]{"fabric-server-launch.jar","minecraft-server.jar",first,second},"jar","fabric-server-launch.jar");
        CheckPackLaunchCase(scratch,"single-jar-priority",new[]{"paper-server.jar",first,second},"jar","paper-server.jar");
    }
    static int PackCheck(string[] args)
    {
        if(args.Length!=2)return 2;string scratch=Path.GetFullPath(args[1]);Directory.CreateDirectory(scratch);try{
            foreach(string bad in new[]{"http://example.invalid/pack.zip","https://user:pass@example.invalid/pack.zip","file:///C:/pack.zip"}){bool blocked=false;try{DownloadUri(bad);}catch(ArgumentException){blocked=true;}Require(blocked,"insecure download URL rejected");}
            foreach(string name in new[]{"../escape.txt","C:/escape.txt","file.txt:stream","NUL.txt","trailing. ","folder/../escape.txt"}){
                string zip=Path.Combine(scratch,Guid.NewGuid().ToString("N")+".zip");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create))using(var writer=new StreamWriter(archive.CreateEntry(name).Open()))writer.Write("unsafe");bool blocked=false;try{ExtractPack(zip,zip+".out",CancellationToken.None,p=>{});}catch(InvalidDataException){blocked=true;}Require(blocked,"unsafe archive member rejected");
            }
            string valid=Path.Combine(scratch,"valid.zip");using(var archive=ZipFile.Open(valid,ZipArchiveMode.Create)){using(var writer=new StreamWriter(archive.CreateEntry("pack/server.jar").Open()))writer.Write("synthetic jar, never executed");using(var writer=new StreamWriter(archive.CreateEntry("pack/server.properties").Open()))writer.Write("server-port=12345\nserver-ip=0.0.0.0\nonline-mode=false\nenable-rcon=true\n");}
            string unpack=Path.Combine(scratch,"unpacked"),workspace=Path.Combine(scratch,"workspace");ExtractPack(valid,unpack,CancellationToken.None,p=>{});Directory.CreateDirectory(workspace);PreparePack(unpack,workspace,"Fixture",25569,null,"");string properties=File.ReadAllText(Path.Combine(workspace,"server","server.properties"));Require(properties.Contains("server-ip=127.0.0.1")&&properties.Contains("online-mode=true")&&properties.Contains("enable-rcon=false")&&properties.Contains("white-list=true"),"safe imported defaults");Require(ValidWorkspace(workspace),"nested server ZIP recognized");
            var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;try{ExtractPack(valid,Path.Combine(scratch,"cancelled"),cancel.Token,p=>{});}catch(OperationCanceledException){cancelled=true;}Require(cancelled,"archive cancellation");
            CheckPackLaunchSelection(scratch);
            string mods=Path.Combine(workspace,"server","mods"),config=Path.Combine(workspace,"server","config");Directory.CreateDirectory(mods);Directory.CreateDirectory(Path.Combine(config,"voicechat"));Directory.CreateDirectory(Path.Combine(config,"plasmovoice","server"));File.WriteAllBytes(Path.Combine(mods,"voicechat-fixture.jar"),new byte[0]);File.WriteAllBytes(Path.Combine(mods,"plasmovoice-fixture.jar"),new byte[0]);string svc=Path.Combine(config,"voicechat","voicechat-server.properties"),plasmo=Path.Combine(config,"plasmovoice","server","config.toml");File.WriteAllText(svc,"port=24460\nvoice_host=\nkeep=true\n");File.WriteAllText(plasmo,"[host]\nip=\"0.0.0.0\"\nport=0\n\n[host.public]\nip=\"old.example.invalid\"\nport=24454\nkeep=true\n");var voice=VoiceEndpoints(Path.Combine(workspace,"server"),25569);Require(voice.Count==2&&voice.Any(v=>v.Name=="Simple Voice Chat"&&v.Port==24460)&&voice.Any(v=>v.Name=="Plasmo Voice"&&v.Port==25569),"voice UDP port detection");foreach(var item in voice){Require(WriteVoiceAddress(item,"voice.example.invalid:31234"),"voice address write");Require(!WriteVoiceAddress(item,"voice.example.invalid:31234"),"idempotent voice update");Require(File.Exists(item.Config+".before-craftharbor.bak")&&File.ReadAllText(item.Config).Contains("keep=true"),"voice backup and unrelated settings preserved");}Require(!VoiceAddressValid("voice.example.invalid:65536")&&!VoiceAddressValid("voice.example.invalid")&&!VoiceAddressValid("https://voice.example.invalid:31234"),"invalid voice endpoints rejected");
            File.WriteAllText(Path.Combine(scratch,"pack-verification.txt"),"PASS: HTTPS checks; unsafe ZIP paths/names rejected; nested server ZIP import defaults and cancellation; unambiguous argument-file launch selection and JAR precedence; Simple Voice Chat/Plasmo Voice UDP detection, address validation, idempotent updates and config backup/preservation. No network requests or downloaded code was executed.");return 0;
        }catch(Exception e){File.WriteAllText(Path.Combine(scratch,"pack-verification.txt"),"FAIL: "+e);return 1;}
    }
}
