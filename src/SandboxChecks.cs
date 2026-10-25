using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Web.Script.Serialization;

partial class CraftHarbor
{
    static void SandboxExpectRejected(Action action,string reason)
    {
        bool rejected=false;try{action();}catch(IOException){rejected=true;}catch(InvalidDataException){rejected=true;}catch(ArgumentException){rejected=true;}Require(rejected,reason);
    }
    static void SandboxFixture(string workspace,string jar,string version)
    {
        string server=Path.Combine(workspace,"server");Directory.CreateDirectory(server);Directory.CreateDirectory(Path.Combine(server,"mods"));
        File.WriteAllText(Path.Combine(server,"server.jar"),jar);
        File.WriteAllText(Path.Combine(server,"server.properties"),"server-port=25565\nserver-ip=0.0.0.0\nlevel-name=world\nonline-mode=true\nwhite-list=true\nenforce-whitelist=true\nenable-rcon=true\nenable-query=true\ncustom=preserve\n");
        File.WriteAllText(Path.Combine(server,"eula.txt"),"eula=true\n");
        File.WriteAllText(Path.Combine(workspace,"manager-profile.json"),new JavaScriptSerializer().Serialize(new ServerProfile{Name="Synthetic "+version,PackVersion=version}));
        Directory.CreateDirectory(Path.Combine(server,"world"));File.WriteAllText(Path.Combine(server,"world","level.dat"),"Synthetic world "+version);
    }
    static void RunSandboxChecks()
    {
        string temp=Path.Combine(Path.GetTempPath(),"CraftHarborSandboxChecks",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        string original=Path.Combine(temp,"original"),next=Path.Combine(temp,"next"),destination=Path.Combine(temp,"trial");
        SandboxFixture(original,"old server bytes","1");SandboxFixture(next,"new server bytes","2");
        File.WriteAllText(Path.Combine(original,"server","mods","same.jar"),"unchanged");File.WriteAllText(Path.Combine(next,"server","mods","same.jar"),"unchanged");
        File.WriteAllText(Path.Combine(original,"server","mods","old.jar"),"removed");File.WriteAllText(Path.Combine(next,"server","mods","new.jar"),"added");
        File.WriteAllText(Path.Combine(original,"server","whitelist.json"),"[{\"name\":\"SyntheticPlayer\"}]");
        File.WriteAllText(Path.Combine(original,"manager-settings.txt"),"12\nprivate.invalid:25565\n");
        Directory.CreateDirectory(Path.Combine(next,"playit"));File.WriteAllText(Path.Combine(next,"playit","agent-secret.toml"),"synthetic-secret-must-not-copy");
        File.WriteAllText(Path.Combine(next,"boot-options.json"),"{\"Enabled\":true}");
        File.WriteAllText(Path.Combine(next,"connection-settings.json"),"{\"Provider\":\"ngrok\",\"VoiceAddress\":\"private.invalid:24454\"}");
        File.WriteAllText(Path.Combine(next,"manager-options.json"),"{\"scheduledRestart\":true,\"autoCrashRestart\":true}");
        string oldWorld=File.ReadAllText(Path.Combine(original,"server","world","level.dat")),oldProps=File.ReadAllText(Path.Combine(original,"server","server.properties"));
        string nextWorld=File.ReadAllText(Path.Combine(next,"server","world","level.dat"));
        SandboxExpectRejected(delegate{PlanUpdateSandbox(original,next,destination,25565);},"sandbox requires a distinct game port");
        SandboxExpectRejected(delegate{PlanUpdateSandbox(original,next,Path.Combine(original,"server","trial"),25566);},"sandbox cannot be nested inside live server");
        SandboxExpectRejected(delegate{PlanUpdateSandbox(original,original,destination,25566);},"template must be a separate workspace");
        SandboxExpectRejected(delegate{PlanUpdateSandbox(original,next,original,25566);},"existing original is never overwritten");
        SandboxExpectRejected(delegate{SandboxBudget(1024,1024,4);},"disk reserve includes original-preserving copy");
        SandboxExpectRejected(delegate{SandboxBudget(long.MaxValue,0,4);},"disk sum cannot overflow");
        SandboxExpectRejected(delegate{SandboxBudget(1024,1073745920L,3);},"insufficient RAM blocks the trial");
        SandboxBudget(1024,1073745920L,4);
        ValidateSandboxOriginalRecovery(new BootOptions{Enabled=false},true,false);
        SandboxExpectRejected(delegate{ValidateSandboxOriginalRecovery(new BootOptions{Enabled=true},true,false);},"old production boot recovery must be disabled before promotion");
        SandboxExpectRejected(delegate{ValidateSandboxOriginalRecovery(new BootOptions{Enabled=false},true,true);},"an enabled leftover Windows task blocks promotion");
        SandboxExpectRejected(delegate{ValidateSandboxOriginalRecovery(new BootOptions{Enabled=false},false,false);},"unknown old boot task state blocks promotion");
        var plan=PlanUpdateSandbox(original,next,destination,25566);Require(plan.Bytes>0&&plan.Worlds.SequenceEqual(new[]{"world"}),"world copy and disk bytes planned");
        var info=MaterializeUpdateSandbox(plan,CancellationToken.None);
        Require(ValidWorkspace(destination)&&info.Phase=="trial"&&!info.ReadyObserved,"new trial created without assuming a successful startup");
        Require(File.ReadAllText(Path.Combine(destination,"server","world","level.dat"))==oldWorld,"trial contains original world, not template world");
        Require(File.ReadAllText(Path.Combine(destination,"server","server.jar"))=="new server bytes","trial uses updated server binaries");
        Require(File.ReadAllText(Path.Combine(original,"server","world","level.dat"))==oldWorld&&File.ReadAllText(Path.Combine(original,"server","server.properties"))==oldProps&&File.ReadAllText(Path.Combine(next,"server","world","level.dat"))==nextWorld,"source world and both input workspaces are unchanged");
        var props=BootProperties(Path.Combine(destination,"server"));Require(props["server-port"]=="25566"&&props["server-ip"]=="127.0.0.1"&&props["enable-rcon"]=="false"&&props["enable-query"]=="false","trial is loopback-only with separate game port and no RCON/query");
        Require(props["online-mode"]=="true"&&props["white-list"]=="true"&&props["custom"]=="preserve","original authentication and template custom settings preserved");
        var hosting=ReadBootJson<HostingOptions>(Path.Combine(destination,"connection-settings.json"));Require(hosting.Provider=="direct"&&!hosting.AutoVoice&&hosting.VoiceAddress=="","trial does not configure a public tunnel or voice rewrite");
        Require(!Directory.Exists(Path.Combine(destination,"playit"))&&!File.Exists(Path.Combine(destination,"boot-options.json"))&&!File.ReadAllText(Path.Combine(destination,"manager-settings.txt")).Contains("private"),"credentials, boot access and saved public address are not copied");
        Require(File.ReadAllText(Path.Combine(destination,"manager-options.json")).Contains("\"scheduledRestart\":false"),"trial schedules and crash loops default disabled");
        Require(info.ModChanges.Any(n=>n=="削除: mods/old.jar")&&info.ModChanges.Any(n=>n=="追加: mods/new.jar")&&!info.ModChanges.Any(n=>n.Contains("same.jar")),"MOD diff includes additions and removals but not equal bytes");
        Require(ReadSandboxInfo(destination).OriginalWorkspace==CanonicalWorkspace(original),"rollback points to original unchanged workspace");
        SandboxExpectRejected(delegate{ValidateSandboxPromotion(info,new HostReply{Session="session",ServerStart=1,ServerExitCode=0},SandboxTestSignature(destination));},"untested trial cannot be promoted");
        info.ReadyObserved=true;info.TestSession="session";info.TestStart=1;info.TestSignature=SandboxTestSignature(destination);
        var stopped=new HostReply{Session="session",ServerStart=1,ServerExitCode=0};ValidateSandboxPromotion(info,stopped,info.TestSignature);
        SandboxExpectRejected(delegate{ValidateSandboxPromotion(info,new HostReply{Session="different",ServerStart=1,ServerExitCode=0},info.TestSignature);},"host restart invalidates recorded test");
        SandboxExpectRejected(delegate{ValidateSandboxPromotion(info,new HostReply{Session="session",ServerStart=1,ServerExitCode=1},info.TestSignature);},"failed exit blocks promotion");
        SandboxExpectRejected(delegate{ValidateSandboxPromotion(info,new HostReply{Session="session",ServerStart=1,ServerPid=123,ServerExitCode=0},info.TestSignature);},"live process blocks promotion");
        SandboxExpectRejected(delegate{ValidateSandboxPromotion(info,null,info.TestSignature);},"unknown exit blocks promotion");
        SandboxExpectRejected(delegate{ValidateSandboxPromotion(info,new HostReply{Session="session",ServerStart=2,ServerExitCode=0},info.TestSignature);},"a different run on the same host invalidates readiness evidence");
        File.AppendAllText(Path.Combine(destination,"server","server.jar"),"changed");
        SandboxExpectRejected(delegate{ValidateSandboxPromotion(info,stopped,SandboxTestSignature(destination));},"changed launch JAR invalidates trial result");
        Directory.CreateDirectory(Path.Combine(destination,"server","bin"));File.WriteAllText(Path.Combine(destination,"server","bin","custom.jar"),"nested launcher");
        File.WriteAllText(Path.Combine(destination,"manager-profile.json"),new JavaScriptSerializer().Serialize(new ServerProfile{JarFile="bin/custom.jar"}));info.TestSignature=SandboxTestSignature(destination);File.AppendAllText(Path.Combine(destination,"server","bin","custom.jar"),"changed");
        SandboxExpectRejected(delegate{ValidateSandboxPromotion(info,stopped,SandboxTestSignature(destination));},"nested profile launch JAR is part of tested signature");
        File.WriteAllText(Path.Combine(original,"server","server.properties"),oldProps.Replace("level-name=world","level-name=../outside"));
        SandboxExpectRejected(delegate{PlanUpdateSandbox(original,next,Path.Combine(temp,"escape-trial"),25566);},"external world path is not copied");File.WriteAllText(Path.Combine(original,"server","server.properties"),oldProps);
        var cancelled=new CancellationTokenSource();cancelled.Cancel();
        SandboxExpectRejected(delegate{MaterializeUpdateSandbox(PlanUpdateSandbox(original,next,Path.Combine(temp,"cancelled"),25566),cancelled.Token);},"cancelled copy never publishes a partial workspace");Require(!Directory.Exists(Path.Combine(temp,"cancelled")),"cancel leaves no accepted destination");cancelled.Dispose();
        // Known voice integrations get distinct local ports; no network traffic is sent.
        string voice=Path.Combine(temp,"voice");Directory.CreateDirectory(Path.Combine(voice,"mods"));Directory.CreateDirectory(Path.Combine(voice,"config","voicechat"));
        File.WriteAllText(Path.Combine(voice,"mods","voicechat.jar"),"synthetic");File.WriteAllText(Path.Combine(voice,"config","voicechat","voicechat-server.properties"),"port=24454\nbind_address=*\nvoice_host=private.invalid:24454\n");
        SandboxVoiceLocal(voice,25566);string voiceText=File.ReadAllText(Path.Combine(voice,"config","voicechat","voicechat-server.properties"));Require(voiceText.Contains("port=25567")&&voiceText.Contains("bind_address=127.0.0.1")&&!voiceText.Contains("private.invalid"),"voice uses separate loopback UDP port without original public address");
        File.Delete(Path.Combine(voice,"config","voicechat","voicechat-server.properties"));SandboxExpectRejected(delegate{SandboxVoiceLocal(voice,25566);},"uninitialized voice config is not allowed to default to a colliding public listener");
        info.TestSignature=SandboxTestSignature(destination);ValidateSandboxPromotion(info,stopped,info.TestSignature);ApplySandboxPromotion(info);
        var promoted=ReadSandboxInfo(destination);var promotedProperties=BootProperties(Path.Combine(destination,"server"));
        Require(promoted.Phase=="promoted"&&promotedProperties["server-port"]=="25565"&&promotedProperties["server-ip"]=="0.0.0.0","validated promotion restores only the old game binding on the new workspace");
        Require(File.Exists(Path.Combine(destination,"server","server.properties.before-promotion.bak"))&&File.ReadAllText(Path.Combine(original,"server","server.properties"))==oldProps&&File.ReadAllText(Path.Combine(original,"server","world","level.dat"))==oldWorld,"promotion keeps trial property backup and rollback original unchanged");
        SandboxExpectRejected(delegate{ValidateSandboxPromotion(promoted,stopped,info.TestSignature);},"a promoted copy cannot be promoted twice");
        File.WriteAllText(Path.Combine(temp,"sandbox-verification.txt"),"PASS: synthetic immutable original/template copy, world selection, alternate loopback ports, RCON/query disabled, original auth preserved, no public credentials/boot/schedule copying, disk/RAM budget rejection, MOD diff, readiness/normal-exit/last-run/config-signature promotion gates, old boot recovery/task rejection, world traversal rejection, cancellation and voice isolation. No Java/server/tunnel launch or external service used.");
    }
}
