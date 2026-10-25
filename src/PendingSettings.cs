using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class PendingServerSettings
{
    public Dictionary<string,string> Properties=new Dictionary<string,string>();
    public ServerProfile Profile;
    public bool HasJavaPath;
    public string JavaPath="",SavedUtc="";
}
partial class CraftHarbor
{
    Label pendingSettingsStatus;
    static string PendingSettingsPath(string workspace){return Path.Combine(workspace,"pending-server-settings.json");}
    static T WithSettingsLock<T>(string workspace,Func<T> action)
    {
        string name="Global\\CraftHarborSettings-"+CurrentSid()+"-"+WorkspaceMutexName(workspace).Substring(6);
        using(var gate=new Mutex(false,name)){
            bool taken=false;
            try {
                try{taken=gate.WaitOne(5000);}catch(AbandonedMutexException){taken=true;}
                if(!taken)throw new IOException("設定の保存中です。少し待ってもう一度お試しください。");
                return action();
            }finally{if(taken)gate.ReleaseMutex();}
        }
    }
    static PendingServerSettings ReadPendingSettings(string workspace)
    {
        var pending=ReadBootJson<PendingServerSettings>(PendingSettingsPath(workspace));
        if(pending==null||pending.Properties==null)throw new InvalidDataException("次回起動用の設定を読み取れません。pending-server-settings.jsonを確認してください。");
        return pending;
    }
    static void StageServerSettings(string workspace,Action<PendingServerSettings> update)
    {
        WithSettingsLock(workspace,delegate{
            var pending=ReadPendingSettings(workspace);update(pending);pending.SavedUtc=DateTime.UtcNow.ToString("o");
            AtomicManagerWrite(PendingSettingsPath(workspace),new JavaScriptSerializer().Serialize(pending));return 0;
        });
    }
    static bool HasPendingSettings(PendingServerSettings value){return value.Properties.Count>0||value.Profile!=null||value.HasJavaPath;}
    PendingServerSettings PendingSettingsForEditor()
    {
        try{return ReadPendingSettings(root);}catch{return new PendingServerSettings();}
        // The status label reports the read error; save/start still use the strict reader and preserve the damaged draft.
    }
    Dictionary<string,string> EditingServerProperties()
    {
        var values=ReadProperties();foreach(var pair in PendingSettingsForEditor().Properties)values[pair.Key]=pair.Value;return values;
    }
    ServerProfile EditingProfile(){return PendingSettingsForEditor().Profile??profile;}
    void RefreshPendingSettingsStatus()
    {
        if(pendingSettingsStatus==null)return;
        try{pendingSettingsStatus.Text=HasPendingSettings(ReadPendingSettings(root))?
            "保存済み・再起動待ち：次回のサーバー起動前に反映します。今の稼働状態は変更しません。":
            "稼働中でも次回起動用に保存できます。変更の反映にはサーバーの再起動が必要です。";}
        catch(Exception error){pendingSettingsStatus.Text=error.Message;}
    }
    void AddPendingSettingsStatus(FlowLayoutPanel form)
    {
        pendingSettingsStatus=new Label{AutoSize=true,MaximumSize=new Size(740,0),ForeColor=Ink,AccessibleName="設定の保存・反映状態",Margin=new Padding(0,8,0,14)};
        form.Controls.Add(pendingSettingsStatus);form.Controls.SetChildIndex(pendingSettingsStatus,0);RefreshPendingSettingsStatus();
    }
    static string PatchServerProperties(string original,Dictionary<string,string> changes)
    {
        var allowed=new HashSet<string>(new[]{"motd","difficulty","gamemode","max-players","view-distance","simulation-distance","pvp","allow-flight","server-port"},StringComparer.Ordinal);
        string text=original;
        foreach(var pair in changes){
            if(!allowed.Contains(pair.Key)||pair.Value==null||pair.Value.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new InvalidDataException("無効なサーバー設定です。");
            string line=pair.Key+"="+EscapeProperty(pair.Value),pattern=@"(?m)^"+Regex.Escape(pair.Key)+@"=[^\r\n]*";
            if(Regex.IsMatch(text,pattern))text=Regex.Replace(text,pattern,delegate(Match match){return line;});
            else text=text.TrimEnd()+"\r\n"+line+"\r\n";
        }
        return text;
    }
    // Caller must hold the workspace maintenance lock and establish that no server is running.
    static bool ApplyPendingServerSettings(string workspace)
    {
        return WithSettingsLock(workspace,delegate{
            var pending=ReadPendingSettings(workspace);if(!HasPendingSettings(pending))return false;
            string directory=Path.Combine(workspace,"server");
            CheckBootPort(directory);
            string portValue;int nextPort;
            if(pending.Properties.TryGetValue("server-port",out portValue)){
                if(!int.TryParse(portValue,out nextPort)||nextPort<1||nextPort>65535)throw new InvalidDataException("次回の接続ポートが無効です。");
                using(var socket=new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,System.Net.Sockets.SocketType.Stream,System.Net.Sockets.ProtocolType.Tcp)){
                    socket.ExclusiveAddressUse=true;socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,nextPort));
                }
            }
            if(pending.Profile!=null)ProfileLaunchTarget(directory,pending.Profile);
            if(pending.HasJavaPath&&pending.JavaPath.Length>0&&(!Path.IsPathRooted(pending.JavaPath)||!File.Exists(pending.JavaPath)))throw new FileNotFoundException("次回起動用のJavaが見つかりません。");
            string path=Path.Combine(directory,"server.properties"),original=File.ReadAllText(path);
            string next=PatchServerProperties(original,pending.Properties);
            // Each file replacement is atomic. Retain the draft on any failure so applying it again is safe.
            if(next!=original){string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temp,next,new UTF8Encoding(false));File.Replace(temp,path,path+".before-settings.bak");}
            if(pending.Profile!=null)AtomicManagerWrite(Path.Combine(workspace,"manager-profile.json"),new JavaScriptSerializer().Serialize(pending.Profile));
            if(pending.HasJavaPath)AtomicManagerWrite(Path.Combine(workspace,"java-path.txt"),pending.JavaPath);
            File.Delete(PendingSettingsPath(workspace));return true;
        });
    }
    void PrepareSavedSettingsForStart()
    {
        EnsureStopped();
        using(var maintenance=WorkspaceMaintenanceLock(root)){
            EnsureStopped();if(ApplyPendingServerSettings(root)){LoadProfile();RefreshProfileLabels();RefreshGuide();Add("[設定] 保存済みの変更を次回起動用ファイルへ反映しました。");}
        }
        RefreshPendingSettingsStatus();
    }
}
