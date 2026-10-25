using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class UpdateSandboxInfo
{
    public int Version=1,Port,OriginalPort;
    public string OriginalWorkspace="",TemplateWorkspace="",Workspace="",CreatedUtc="",Phase="trial",LastResult="未確認",TestSession="",TestSignature="",OriginalBind="";
    public long TestStart,CopyBytes;
    public bool ReadyObserved;
    public string[] ModChanges=new string[0];
}
class SandboxCopyPlan
{
    public string Original="",Template="",Destination="",LevelName="world";
    public int Port,OriginalPort;
    public long Bytes;
    public string[] Worlds=new string[0];
}

partial class CraftHarbor
{
    static string SandboxInfoPath(string workspace){return Path.Combine(workspace,"update-sandbox.json");}
    static string SandboxProperty(string text,string key,string value)
    {
        if(value==null||value.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new InvalidDataException("設定値を確認してください。");
        string pattern=@"(?m)^[ \t]*"+Regex.Escape(key)+@"[ \t]*=[^\r\n]*";
        string line=key+"="+value;
        return Regex.IsMatch(text,pattern)?Regex.Replace(text,pattern,delegate(Match m){return line;}):text.TrimEnd()+"\r\n"+line+"\r\n";
    }
    static bool SandboxSamePath(string a,string b){return string.Equals(CanonicalWorkspace(a),CanonicalWorkspace(b),StringComparison.OrdinalIgnoreCase);}
    static bool SandboxInside(string child,string parent){return SandboxSamePath(child,parent)||CanonicalWorkspace(child).StartsWith(CanonicalWorkspace(parent)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
    static void SandboxNoLinks(string path)
    {
        string full=Path.GetFullPath(path);
        while(!string.IsNullOrEmpty(full)){
            if((Directory.Exists(full)||File.Exists(full))&&(File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)
                throw new IOException("ジャンクション・シンボリックリンクを含む保存先は使えません。");
            string parent=Path.GetDirectoryName(full);if(parent==full)break;full=parent;
        }
    }
    static IEnumerable<string> SandboxTree(string directory)
    {
        SandboxNoLinks(directory);
        foreach(string path in Directory.GetFileSystemEntries(directory)){
            SandboxNoLinks(path);yield return path;
            if(Directory.Exists(path))foreach(string nested in SandboxTree(path))yield return nested;
        }
    }
    static long SandboxTreeBytes(string path)
    {
        long total=0;int count=0;
        foreach(string item in SandboxTree(path)){
            if(++count>500000)throw new IOException("ファイル数が多すぎます。保存先を確認してください。");
            if(File.Exists(item))total=checked(total+new FileInfo(item).Length);
        }
        return total;
    }
    static string SandboxLevelName(string server)
    {
        string name;var props=BootProperties(server);
        if(!props.TryGetValue("level-name",out name)||string.IsNullOrWhiteSpace(name))name="world";
        // A conservative single-directory name avoids copying an unexpected external/custom world path.
        if(name=="."||name==".."||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.IndexOfAny(new[]{'/','\\',':'})>=0)
            throw new InvalidDataException("level-nameはserver直下のフォルダー名にしてください。外部ワールドは自動コピーしません。");
        return name;
    }
    static string[] SandboxWorlds(string server)
    {
        string level=SandboxLevelName(server);var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string name in new[]{level,level+"_nether",level+"_the_end"})if(Directory.Exists(Path.Combine(server,name)))names.Add(name);
        foreach(string path in Directory.GetDirectories(server))if(File.Exists(Path.Combine(path,"level.dat")))names.Add(Path.GetFileName(path));
        return names.OrderBy(n=>n,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    static SandboxCopyPlan PlanUpdateSandbox(string original,string template,string destination,int port)
    {
        original=CanonicalWorkspace(original);template=CanonicalWorkspace(template);destination=CanonicalWorkspace(destination);
        SandboxNoLinks(original);SandboxNoLinks(template);SandboxNoLinks(destination);
        if(!ValidWorkspace(original)||!ValidWorkspace(template))throw new IOException("本番と更新元に導入済みのserverフォルダーが必要です。");
        if(SandboxInside(template,original)||SandboxInside(original,template))throw new IOException("本番と更新元には重ならない別の保存先を指定してください。");
        if(SandboxInside(destination,Path.Combine(original,"server"))||SandboxInside(destination,template)||SandboxInside(original,destination))throw new IOException("お試し環境はserver/更新元の外に作成してください。");
        if(Directory.Exists(destination)||File.Exists(destination))throw new IOException("新しい保存先を指定してください。既存データは上書きしません。");
        string sourceServer=Path.Combine(original,"server"),templateServer=Path.Combine(template,"server");
        int originalPort=BootPort(sourceServer);if(port<1024||port>65535||port==originalPort)throw new ArgumentException("お試しポートは本番とは異なる1024〜65535にしてください。");
        var sourceWorlds=SandboxWorlds(sourceServer);var ignoredWorlds=new HashSet<string>(SandboxWorlds(templateServer),StringComparer.OrdinalIgnoreCase);
        long bytes=0;
        foreach(string path in Directory.GetFileSystemEntries(templateServer)){
            SandboxNoLinks(path);if(ignoredWorlds.Contains(Path.GetFileName(path)))continue;
            bytes=checked(bytes+(Directory.Exists(path)?SandboxTreeBytes(path):new FileInfo(path).Length));
        }
        foreach(string name in sourceWorlds)bytes=checked(bytes+SandboxTreeBytes(Path.Combine(sourceServer,name)));
        return new SandboxCopyPlan{Original=original,Template=template,Destination=destination,Port=port,OriginalPort=originalPort,Bytes=bytes,Worlds=sourceWorlds,LevelName=SandboxLevelName(sourceServer)};
    }
    static void SandboxBudget(long copyBytes,long freeBytes,int heapGiB)
    {
        long reserve=Math.Max(1073741824L,copyBytes/20);
        if(copyBytes<0||copyBytes>long.MaxValue-reserve||freeBytes<copyBytes+reserve)throw new IOException("原本を残したコピーと1 GiB以上の空きを確保できません。");
        if(heapGiB<4)throw new IOException("OSと他サーバーの予約分を残すと、お試し用の4 GiBヒープを確保できません。");
    }
    static HostReply SandboxHostStatus(string workspace)
    {
        using(var pipe=new NamedPipeClientStream(".",HostPipe(workspace),PipeDirection.InOut,PipeOptions.None)){
            try{pipe.Connect(400);}catch(TimeoutException){return null;}
            using(var writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true){AutoFlush=true})
            using(var reader=new StreamReader(pipe,new UTF8Encoding(false),false,4096,true)){
                writer.WriteLine(new JavaScriptSerializer().Serialize(new HostRequest{Since=long.MaxValue}));
                var pending=reader.ReadLineAsync();if(!pending.Wait(2000))throw new IOException("常駐ホストの応答を待っています。操作を中止しました。");string json=pending.GetAwaiter().GetResult();if(json==null||json.Length>4194304)throw new IOException("常駐ホストの停止状態を確認できません。");
                var reply=new JavaScriptSerializer{MaxJsonLength=4194304}.Deserialize<HostReply>(json);
                if(reply==null||reply.Protocol!=1||!string.IsNullOrEmpty(reply.Error))throw new IOException("常駐ホストの状態を確認できません。");return reply;
            }
        }
    }
    sealed class SandboxLease : IDisposable
    {
        FileStream maintenance;Mutex manager;bool acquired;Socket port,port6;
        public SandboxLease(string workspace,bool managerMayBeOpen,HashSet<int> heldPorts)
        {
            try{
                SandboxNoLinks(workspace);
                if(!managerMayBeOpen){manager=new Mutex(false,WorkspaceMutexName(workspace));try{acquired=manager.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}if(!acquired)throw new IOException("更新元・お試し環境の管理画面を閉じてから操作してください。");}
                maintenance=WorkspaceMaintenanceLock(workspace);
                var status=SandboxHostStatus(workspace);
                if(status!=null&&(status.BootInitializing||status.Maintenance||status.ServerPid>0||status.TunnelPid>0))throw new IOException("本番・更新元・お試し環境のサーバーと公開接続を停止してください。");
                int number=BootPort(Path.Combine(workspace,"server"));
                if(heldPorts.Add(number)){port=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);port.ExclusiveAddressUse=true;port.Bind(new IPEndPoint(IPAddress.Any,number));if(Socket.OSSupportsIPv6){port6=new Socket(AddressFamily.InterNetworkV6,SocketType.Stream,ProtocolType.Tcp);port6.ExclusiveAddressUse=true;port6.DualMode=false;port6.Bind(new IPEndPoint(IPAddress.IPv6Any,number));}}
            }catch{Dispose();throw;}
        }
        public void Dispose(){if(port6!=null){port6.Dispose();port6=null;}if(port!=null){port.Dispose();port=null;}if(maintenance!=null){maintenance.Dispose();maintenance=null;}if(manager!=null){if(acquired){manager.ReleaseMutex();acquired=false;}manager.Dispose();manager=null;}}
    }
    static void CopySandboxTree(string source,string destination,CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();SandboxNoLinks(source);SandboxNoLinks(destination);Directory.CreateDirectory(destination);
        foreach(string path in Directory.GetFileSystemEntries(source)){
            cancel.ThrowIfCancellationRequested();SandboxNoLinks(path);string target=Path.Combine(destination,Path.GetFileName(path));
            if(Directory.Exists(path))CopySandboxTree(path,target,cancel);else File.Copy(path,target,false);
        }
    }
    static Dictionary<string,string> SandboxMods(string workspace)
    {
        var mods=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(string folder in new[]{"mods","plugins"}){
            string dir=Path.Combine(workspace,"server",folder);if(!Directory.Exists(dir))continue;
            foreach(string path in Directory.GetFiles(dir,"*.jar")){SandboxNoLinks(path);mods[folder+"/"+Path.GetFileName(path)]=Sha256File(path);}
        }
        return mods;
    }
    static string[] SandboxModChanges(string oldWorkspace,string newWorkspace)
    {
        var before=SandboxMods(oldWorkspace);var after=SandboxMods(newWorkspace);var result=new List<string>();
        foreach(string name in before.Keys.Union(after.Keys,StringComparer.OrdinalIgnoreCase).OrderBy(n=>n,StringComparer.OrdinalIgnoreCase)){
            if(!before.ContainsKey(name))result.Add("追加: "+name);else if(!after.ContainsKey(name))result.Add("削除: "+name);else if(before[name]!=after[name])result.Add("変更: "+name);
        }
        return result.ToArray();
    }
    static void WriteSandboxInfo(UpdateSandboxInfo info)
    {
        string path=SandboxInfoPath(info.Workspace),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        File.WriteAllText(temp,new JavaScriptSerializer{MaxJsonLength=4194304}.Serialize(info),new UTF8Encoding(false));
        if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
    }
    static UpdateSandboxInfo ReadSandboxInfo(string workspace)
    {
        SandboxNoLinks(workspace);string path=SandboxInfoPath(workspace);SandboxNoLinks(path);
        if(!File.Exists(path)||new FileInfo(path).Length>4194304)throw new IOException("お試し環境の記録がありません。");
        var value=new JavaScriptSerializer{MaxJsonLength=4194304}.Deserialize<UpdateSandboxInfo>(File.ReadAllText(path));
        if(value==null||value.Version!=1||value.Port<1024||value.Port>65535||value.OriginalPort<1||value.OriginalPort>65535||string.IsNullOrWhiteSpace(value.Workspace)||string.IsNullOrWhiteSpace(value.OriginalWorkspace)||!SandboxSamePath(value.Workspace,workspace)||SandboxSamePath(value.Workspace,value.OriginalWorkspace))throw new InvalidDataException("お試し環境の記録を確認してください。");
        SandboxNoLinks(value.OriginalWorkspace);if(value.ModChanges==null)value.ModChanges=new string[0];return value;
    }
    static string SandboxTestSignature(string workspace)
    {
        var lines=new List<string>();string server=Path.Combine(workspace,"server");
        foreach(string jar in Directory.GetFiles(server,"*.jar")){SandboxNoLinks(jar);lines.Add("server/"+Path.GetFileName(jar)+"="+Sha256File(jar));}
        string libraries=Path.Combine(server,"libraries");if(Directory.Exists(libraries))foreach(string item in SandboxTree(libraries).Where(File.Exists).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase))lines.Add(item.Substring(server.Length+1)+"="+Sha256File(item));
        var selected=ReadBootJson<ServerProfile>(Path.Combine(workspace,"manager-profile.json"));if(selected==null)throw new InvalidDataException("起動プロファイルを確認してください。");
        ProfileLaunchTarget(server,selected);string launch=ServerRelativePath(server,selected.LaunchMode=="argfile"?selected.ArgumentsFile:selected.JarFile);SandboxNoLinks(launch);lines.Add("launch-target="+Sha256File(launch));
        var mods=SandboxMods(workspace);foreach(var item in mods.OrderBy(p=>p.Key,StringComparer.OrdinalIgnoreCase))lines.Add(item.Key+"="+item.Value);
        foreach(string name in new[]{"manager-profile.json","java-path.txt","java-options.json","server/server.properties"}){
            string path=Path.Combine(workspace,name.Replace('/',Path.DirectorySeparatorChar));if(File.Exists(path)){SandboxNoLinks(path);lines.Add(name+"="+Sha256File(path));}
        }
        using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n",lines)))).Replace("-","");
    }
    static void SandboxVoiceLocal(string server,int gamePort)
    {
        var endpoints=VoiceEndpoints(server,gamePort);int next=gamePort;
        var names=new List<string>();foreach(string folder in new[]{"mods","plugins"}){string directory=Path.Combine(server,folder);if(Directory.Exists(directory))names.AddRange(Directory.GetFiles(directory,"*.jar").Select(p=>Regex.Replace(Path.GetFileName(p).ToLowerInvariant(),"[^a-z0-9]","")));}
        if(names.Any(n=>n.Contains("voicechat"))&&!endpoints.Any(e=>e.Name=="Simple Voice Chat")||names.Any(n=>n.Contains("plasmovoice"))&&!endpoints.Any(e=>e.Name=="Plasmo Voice"))
            throw new InvalidDataException("音声MODの設定ファイルがまだありません。更新元で初期セットアップし、停止してから再試行してください。");
        foreach(var endpoint in endpoints){
            if(next>=65535)next=1024;else next++;
            string text=File.ReadAllText(endpoint.Config);
            if(endpoint.Name=="Simple Voice Chat"){text=SandboxProperty(text,"port",next.ToString());text=SandboxProperty(text,"bind_address","127.0.0.1");text=SandboxProperty(text,"voice_host","");}
            else{
                var section=new Regex(@"(?ms)^\s*\[host\][^\r\n]*\r?\n(.*?)(?=^\s*\[|\z)");var match=section.Match(text);
                if(!match.Success)throw new InvalidDataException("音声MODのhost設定を確認できません。");
                string body=Regex.Replace(match.Groups[1].Value,@"(?m)^\s*(?:ip|port)\s*=.*(?:\r?\n|$)","");
                text=section.Replace(text,"[host]\r\nip = \"127.0.0.1\"\r\nport = "+next+"\r\n"+body,1);
                text=Regex.Replace(text,@"(?ms)^\s*\[host\.public\][^\r\n]*\r?\n.*?(?=^\s*\[|\z)","");
            }
            File.WriteAllText(endpoint.Config,text,new UTF8Encoding(false));
        }
    }
    // File-only phase is separate so synthetic checks never need Java, a server, or external services.
    static UpdateSandboxInfo MaterializeUpdateSandbox(SandboxCopyPlan plan,CancellationToken cancel)
    {
        string parent=Path.GetDirectoryName(plan.Destination);Directory.CreateDirectory(parent);SandboxNoLinks(parent);
        string stage=Path.Combine(parent,".craftharbor-sandbox-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        string stageServer=Path.Combine(stage,"server"),newServer=Path.Combine(plan.Template,"server"),oldServer=Path.Combine(plan.Original,"server");
        var ignored=new HashSet<string>(SandboxWorlds(newServer),StringComparer.OrdinalIgnoreCase);Directory.CreateDirectory(stageServer);
        try{
            foreach(string path in Directory.GetFileSystemEntries(newServer)){
                cancel.ThrowIfCancellationRequested();SandboxNoLinks(path);if(ignored.Contains(Path.GetFileName(path)))continue;string target=Path.Combine(stageServer,Path.GetFileName(path));
                if(Directory.Exists(path))CopySandboxTree(path,target,cancel);else File.Copy(path,target,false);
            }
            foreach(string name in plan.Worlds){string target=Path.Combine(stageServer,name);if(Directory.Exists(target)||File.Exists(target))throw new IOException("更新元のワールド名とファイルが重複しています："+name);CopySandboxTree(Path.Combine(oldServer,name),target,cancel);}
            foreach(string name in new[]{"whitelist.json","ops.json","banned-players.json","banned-ips.json"})if(File.Exists(Path.Combine(oldServer,name))){SandboxNoLinks(Path.Combine(oldServer,name));File.Copy(Path.Combine(oldServer,name),Path.Combine(stageServer,name),true);}
            string propsPath=Path.Combine(stageServer,"server.properties"),props=File.ReadAllText(propsPath);
            var sourceProperties=BootProperties(oldServer);string securityValue;
            foreach(string key in new[]{"online-mode","white-list","enforce-whitelist"}){if(sourceProperties.TryGetValue(key,out securityValue))props=SandboxProperty(props,key,securityValue);else if(key=="online-mode")props=SandboxProperty(props,key,"true");}
            props=SandboxProperty(props,"level-name",plan.LevelName);props=SandboxProperty(props,"server-ip","127.0.0.1");props=SandboxProperty(props,"server-port",plan.Port.ToString());props=SandboxProperty(props,"enable-rcon","false");props=SandboxProperty(props,"enable-query","false");
            File.WriteAllText(propsPath,props,new UTF8Encoding(false));SandboxVoiceLocal(stageServer,plan.Port);
            foreach(string name in new[]{"manager-profile.json","java-path.txt","java-options.json","performance-settings.json"})if(File.Exists(Path.Combine(plan.Template,name))){SandboxNoLinks(Path.Combine(plan.Template,name));File.Copy(Path.Combine(plan.Template,name),Path.Combine(stage,name),false);}
            var selected=ReadBootJson<ServerProfile>(Path.Combine(stage,"manager-profile.json"));if(selected==null)throw new InvalidDataException("更新元のプロファイルを確認してください。");selected.Name=(selected.Name??"Minecraft Server")+" / 更新のお試し";
            ProfileLaunchTarget(stageServer,selected);
            File.WriteAllText(Path.Combine(stage,"manager-profile.json"),new JavaScriptSerializer().Serialize(selected),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(stage,"manager-options.json"),"{\"autoMemory\":false,\"autoCrashRestart\":false,\"scheduledRestart\":false}",new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(stage,"manager-settings.txt"),"4\r\n\r\n",new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(stage,"connection-settings.json"),new JavaScriptSerializer().Serialize(new HostingOptions{Provider="direct",AutoVoice=false}),new UTF8Encoding(false));
            var originalProps=BootProperties(oldServer);string bind;originalProps.TryGetValue("server-ip",out bind);
            var info=new UpdateSandboxInfo{OriginalWorkspace=plan.Original,TemplateWorkspace=plan.Template,Workspace=plan.Destination,Port=plan.Port,OriginalPort=plan.OriginalPort,OriginalBind=bind??"",CreatedUtc=DateTime.UtcNow.ToString("o"),CopyBytes=plan.Bytes,ModChanges=SandboxModChanges(plan.Original,stage)};
            File.WriteAllText(SandboxInfoPath(stage),new JavaScriptSerializer{MaxJsonLength=4194304}.Serialize(info),new UTF8Encoding(false));cancel.ThrowIfCancellationRequested();Directory.Move(stage,plan.Destination);return info;
        }catch(Exception error){throw new IOException("お試し環境を作成できませんでした。原本と更新元は変更していません。一時コピー："+stage+"\r\n"+error.Message,error);}
    }
    static UpdateSandboxInfo CreateUpdateSandbox(string original,string template,string destination,int port,bool originalManagerOpen,CancellationToken cancel)
    {
        var held=new HashSet<int>();using(var sourceLease=new SandboxLease(original,originalManagerOpen,held))using(var templateLease=new SandboxLease(template,false,held)){
            var plan=PlanUpdateSandbox(original,template,destination,port);
            using(var gate=new Mutex(false,MemoryGateName())){
                bool acquired=false;try{try{acquired=gate.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}if(!acquired)throw new IOException("他サーバーのメモリ計算を待ってください。");
                    var memory=PhysicalMemory();double limits=0,uncommitted=0;foreach(var reservation in LiveReservations()){limits+=reservation.HeapGiB;try{using(var p=Process.GetProcessById(reservation.Pid))uncommitted+=Math.Max(0,reservation.HeapGiB-p.WorkingSet64/1073741824.0);}catch(ArgumentException){}}
                    int budget=SharedBudget(memory[0],memory[1],Math.Max(2,RegisteredWorkspaceCount()+1),limits,uncommitted);
                    SandboxBudget(plan.Bytes,new DriveInfo(Path.GetPathRoot(plan.Destination)).AvailableFreeSpace,budget);
                }finally{if(acquired)gate.ReleaseMutex();}
            }
            return MaterializeUpdateSandbox(plan,cancel);
        }
    }
    static UpdateSandboxInfo RecordSandboxResult(string workspace)
    {
        var info=ReadSandboxInfo(workspace);if(info.Phase!="trial")throw new IOException("本番切替済みのお試し環境です。新しいコピーで再試験してください。");
        var status=SandboxHostStatus(workspace);info.ReadyObserved=status!=null&&status.Ready&&status.ServerPid>0&&!status.BootInitializing&&!status.Maintenance;
        info.LastResult=info.ReadyObserved?"起動完了を常駐ホストで確認（ゲーム参加・MOD互換性は別途確認）":status==null?"未確認：常駐ホストなし":status.ServerPid>0?"準備中：起動完了は未確認":"停止中：終了コード "+status.ServerExitCode;
        info.TestSession=info.ReadyObserved?status.Session:"";info.TestStart=info.ReadyObserved?status.ServerStart:0;info.TestSignature=info.ReadyObserved?SandboxTestSignature(workspace):"";WriteSandboxInfo(info);return info;
    }
    static void ValidateSandboxPromotion(UpdateSandboxInfo info,HostReply stoppedStatus,string currentSignature)
    {
        if(info.Phase!="trial"||!info.ReadyObserved||info.TestStart<=0||string.IsNullOrEmpty(info.TestSession)||info.TestSignature!=currentSignature)
            throw new IOException("起動完了の記録がないか、記録後に構成が変わりました。お試し起動と結果記録をやり直してください。");
        if(stoppedStatus==null||stoppedStatus.Session!=info.TestSession||stoppedStatus.ServerStart!=info.TestStart||stoppedStatus.ServerPid>0||stoppedStatus.TunnelPid>0||stoppedStatus.BootInitializing||stoppedStatus.Maintenance||stoppedStatus.ServerExitCode!=0)
            throw new IOException("記録した常駐ホストで保存停止・正常終了を確認できません。再試験してから切り替えてください。");
    }
    static void ValidateSandboxOriginalRecovery(BootOptions options,bool taskStateKnown,bool taskEnabled)
    {
        if(options==null||options.Enabled||!taskStateKnown||taskEnabled)
            throw new IOException("旧本番のWindows自動復帰が有効、またはタスク状態を確認できません。元の管理画面の運用設定で自動復帰を無効化・解除し、閉じてから再試行してください。タスクや資格情報は自動変更しません。");
    }
    static void CheckSandboxOriginalRecovery(string workspace)
    {
        string path=BootOptionsPath(workspace);SandboxNoLinks(path);var options=ReadBootJson<BootOptions>(path);
        // Check the saved intent first, then the current user's exact registered task. Reads only.
        ValidateSandboxOriginalRecovery(options,true,false);
        dynamic service=null,folder=null,task=null;
        try{service=OpenTaskService();folder=service.GetFolder("\\");task=FindBootTask(folder,BootTaskName(workspace,CurrentSid()));ValidateSandboxOriginalRecovery(options,true,task!=null&&(bool)task.Enabled);}
        catch(IOException){throw;}catch(Exception error){throw new IOException("旧本番のWindows自動復帰タスクの状態を確認できないため本番切替を中止しました。元の運用画面で登録状態を確認してください。",error);}
        finally{ReleaseTaskObject(task);ReleaseTaskObject(folder);ReleaseTaskObject(service);}
    }
    static UpdateSandboxInfo PromoteUpdateSandbox(string workspace,bool currentIsOriginal,bool currentIsSandbox)
    {
        var info=ReadSandboxInfo(workspace);var held=new HashSet<int>();
        using(var sourceLease=new SandboxLease(info.OriginalWorkspace,currentIsOriginal,held))using(var trialLease=new SandboxLease(workspace,currentIsSandbox,held)){
            CheckSandboxOriginalRecovery(info.OriginalWorkspace);ValidateSandboxPromotion(info,SandboxHostStatus(workspace),SandboxTestSignature(workspace));
            return ApplySandboxPromotion(info);
        }
    }
    // Called only after stopped-process, boot-task and tested-configuration gates while both leases are held.
    static UpdateSandboxInfo ApplySandboxPromotion(UpdateSandboxInfo info)
    {
        string propsPath=Path.Combine(info.Workspace,"server","server.properties"),props=File.ReadAllText(propsPath);
        if(!File.Exists(propsPath+".before-promotion.bak"))File.Copy(propsPath,propsPath+".before-promotion.bak",false);
        props=SandboxProperty(props,"server-port",info.OriginalPort.ToString());props=SandboxProperty(props,"server-ip",info.OriginalBind??"");
        string propertyTemp=propsPath+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(propertyTemp,props,new UTF8Encoding(false));File.Replace(propertyTemp,propsPath,null);
        info.Phase="promoted";info.LastResult="新しい保存先を本番に切替。旧原本は未変更。公開接続・音声・予約は別途確認。";WriteSandboxInfo(info);return info;
    }
    static string SandboxSummary(UpdateSandboxInfo info)
    {
        return "状態: "+info.Phase+" / "+info.LastResult+"\r\nお試し: "+info.Workspace+"\r\n戻す原本: "+info.OriginalWorkspace+"\r\nコピー: "+(info.CopyBytes/1073741824.0).ToString("0.00")+" GiB / ポート: "+info.Port+"\r\n"+(info.ModChanges.Length==0?"MODファイルの差分なし（互換性の保証ではありません）":string.Join("\r\n",info.ModChanges));
    }
    void ShowUpdateSandbox(bool snapshot=false)
    {
        using(var dialog=new HarborDialog{Text="MODパック更新のお試し環境",Size=new Size(990,780),MinimumSize=new Size(850,700),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=new Font("Yu Gothic UI",10)}){
            var form=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(20)};dialog.Controls.Add(form);
            form.Controls.Add(new Label{Text="ワールド原本を残し、別の保存先で更新を試します。",Font=new Font("Yu Gothic UI",17,FontStyle.Bold),AutoSize=true});
            form.Controls.Add(new Label{Text="本番・更新元・お試しのサーバーと公開接続を停止して操作してください。更新元は既存のURL取り込み等で事前に用意します。コピーだけを行い、スクリプトは実行しません。\r\n既定ではloopback・別ポート、公開トンネルなし、RCON/query無効です。独自プラグインの追加待受は別途確認してください。外部ワールドや独自storageは対象外です（server直下のlevel-name/level.datを持つワールドをコピー）。",AutoSize=true,MaximumSize=new Size(890,0),ForeColor=Muted});
            string original=root;if(File.Exists(SandboxInfoPath(root)))try{original=ReadSandboxInfo(root).OriginalWorkspace;}catch{}
            var origin=TextSetting(form,"変更しない本番原本",650);origin.Text=original;origin.ReadOnly=true;
            var template=TextSetting(form,"導入済みの更新元",650);
            var pickTemplate=FlowBtn(form,"更新元を選択",delegate{using(var folder=new FolderBrowserDialog{Description="新バージョンを導入したworkspace（serverの親）",ShowNewFolderButton=false})if(folder.ShowDialog(dialog)==DialogResult.OK)template.Text=folder.SelectedPath;});
            var destination=TextSetting(form,"新しいお試し保存先",650);destination.Text=File.Exists(SandboxInfoPath(root))?root:Path.Combine(original,"sandboxes","update-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            int defaultPort=BootPort(Path.Combine(original,"server"));defaultPort=defaultPort<65534?defaultPort+1:25566;var port=NumberSetting(form,"別のゲームポート",1024,65535,defaultPort);
            var view=new TextBox{Width=890,Height=180,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,BackColor=Card,ForeColor=Ink,AccessibleName="お試し環境の差分と検証結果"};form.Controls.Add(view);
            Action refresh=delegate{if(File.Exists(SandboxInfoPath(destination.Text.Trim())))view.Text=SandboxSummary(ReadSandboxInfo(destination.Text.Trim()));else view.Text="4 GiB以上のRAM予算と、コピー容量＋1 GiB以上の空きを確認して作成します。起動直前にも既存の共有RAM予算を再評価します。";};refresh();
            var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=true,MaximumSize=new Size(890,0)};form.Controls.Add(actions);
            CancellationTokenSource cancellation=null;bool working=false;
            Action<bool> setBusy=delegate(bool busy){foreach(Control c in actions.Controls)c.Enabled=!busy;foreach(Control c in new Control[]{template,destination,port,pickTemplate})c.Enabled=!busy;};
            Button create=null;create=FlowBtn(actions,"コピーを作成",async delegate{
                if(working)return;try{EnsureStopped();if(Running(tunnel))throw new IOException("本番の公開接続も停止してください。");string source=origin.Text,target=destination.Text.Trim(),next=template.Text.Trim();int number=(int)port.Value;cancellation=new CancellationTokenSource();working=true;backupBusy=true;setBusy(true);view.Text="コピーを作成しています。原本は変更しません…";
                    await Task.Run(()=>CreateUpdateSandbox(source,next,target,number,SandboxSamePath(root,source),cancellation.Token));if(!dialog.IsDisposed){refresh();Add("[更新お試し] コピーを作成しました。原本は変更していません。");}
                }catch(Exception error){if(!dialog.IsDisposed)view.Text=error.Message;}finally{backupBusy=false;working=false;if(!dialog.IsDisposed)setBusy(false);if(cancellation!=null){cancellation.Dispose();cancellation=null;}}});
            FlowBtn(actions,"既存の試験を選択",delegate{using(var folder=new FolderBrowserDialog{Description="update-sandbox.jsonがあるお試しworkspace",ShowNewFolderButton=false})if(folder.ShowDialog(dialog)==DialogResult.OK){var info=ReadSandboxInfo(folder.SelectedPath);if(!SandboxSamePath(info.OriginalWorkspace,origin.Text))throw new IOException("この本番原本に対応する試験を選んでください。");destination.Text=folder.SelectedPath;refresh();}});
            FlowBtn(actions,"お試しを別画面で開く",delegate{var info=ReadSandboxInfo(destination.Text.Trim());if(info.Phase!="trial")throw new IOException("本番切替済みです。新しいコピーで再試験してください。");OpenRegistered(info.Workspace,false);});
            FlowBtn(actions,"起動結果を記録",async delegate{if(working)return;string selected=destination.Text.Trim();try{working=true;setBusy(true);view.Text="起動状態とJAR構成を確認しています…";var result=await Task.Run(()=>RecordSandboxResult(selected));if(!dialog.IsDisposed)view.Text=SandboxSummary(result);}catch(Exception error){if(!dialog.IsDisposed)view.Text=error.Message;}finally{working=false;if(!dialog.IsDisposed)setBusy(false);}});
            var verified=new HarborCheckBox{Text="自分で接続・MOD・ワールドを確認済み。変換後の変更は旧原本へ戻りません",AutoSize=true};form.Controls.Add(verified);
            FlowBtn(actions,"このコピーを本番に切替",async delegate{if(working)return;try{if(!verified.Checked)throw new IOException("接続・MOD・ワールドを手動確認してチェックしてください。");EnsureStopped();string selected=destination.Text.Trim(),source=origin.Text;if(!SandboxSamePath(root,selected))throw new IOException("本番切替はお試し環境の管理画面で行います。元の管理画面を閉じ、試験画面は閉じずに保存停止して、この機能を開いてください。");var info=ReadSandboxInfo(selected);if(!SandboxSamePath(info.OriginalWorkspace,source))throw new IOException("本番原本が一致しません。");if(MessageBox.Show(dialog,"旧原本は上書きせず、新しい保存先を本番として開きます。旧本番のWindows自動復帰は事前に無効化・解除してください。\r\nゲームの待受・ポートだけを旧本番へ合わせます。公開接続の資格情報、音声設定、Windows自動復帰・予約は引き継ぎません。既存設定画面で再確認してください。\r\n旧原本に戻すと、試験・切替後のワールド変更は反映されません。","本番切替の確認",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK)return;
                working=true;backupBusy=true;setBusy(true);view.Text="停止状態と試験済み構成を照合しています…";await Task.Run(()=>PromoteUpdateSandbox(selected,SandboxSamePath(root,source),SandboxSamePath(root,selected)));working=false;backupBusy=false;setBusy(false);view.Text=SandboxSummary(ReadSandboxInfo(selected));dialog.Close();if(!SandboxSamePath(root,selected))OpenRegistered(selected,true);
                }catch(Exception error){if(!dialog.IsDisposed)view.Text=error.Message;}finally{working=false;backupBusy=false;if(!dialog.IsDisposed)setBusy(false);}});
            FlowBtn(actions,"旧原本へ戻す",delegate{var info=ReadSandboxInfo(destination.Text.Trim());if(!SandboxSamePath(info.OriginalWorkspace,origin.Text))throw new IOException("本番原本が一致しません。");EnsureStopped();if(SandboxSamePath(root,info.OriginalWorkspace)){view.Text="この画面は旧原本です。新しい保存先は保持しています。";return;}var held=new HashSet<int>();using(var currentLease=new SandboxLease(root,true,held))using(var originalLease=new SandboxLease(info.OriginalWorkspace,SandboxSamePath(root,info.OriginalWorkspace),held)){if(MessageBox.Show(dialog,"旧原本を開きます。新しい保存先は削除せず、切替後の変更を旧ワールドに戻しません。","旧原本へ戻す",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK)return;}dialog.Close();if(!SandboxSamePath(root,info.OriginalWorkspace))OpenRegistered(info.OriginalWorkspace,true);});
            form.Controls.Add(new Label{Text="起動完了はMOD互換性や参加成功の証明ではありません。起動中に結果を記録し、試験画面は閉じずに保存停止してください。元の管理画面を閉じ、試験画面からこの機能を開いて本番切替します。\r\nワールド変換は不可逆な場合があります。完全バックアップと旧原本を保持し、実環境の接続・音声は別途検証してください。",AutoSize=true,MaximumSize=new Size(890,0),ForeColor=Muted});
            FlowBtn(form,"閉じる",delegate{if(working){if(cancellation!=null)cancellation.Cancel();view.Text=cancellation!=null?"中止を待っています…":"照合・切替の完了を待っています…";}else dialog.Close();});
            dialog.FormClosing+=delegate(object sender,FormClosingEventArgs e){if(working){e.Cancel=true;if(cancellation!=null)cancellation.Cancel();}};
            StyleControls(dialog);ThemeDialog(dialog);if(snapshot)CaptureOperationsDialog(dialog,"ui-update-sandbox.png");dialog.ShowDialog(this);
        }
    }
}
