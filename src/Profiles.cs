using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class ServerProfile
{
    public string Name="Minecraft Server";
    public string MinecraftVersion="";
    public string PackName="";
    public string PackVersion="";
    public string ClientMode="vanilla";
    public string ClientUrl="";
    public string LaunchMode="jar";
    public string JarFile="server.jar";
    public string ArgumentsFile="";
    public string SparkMode="auto";
}

partial class CraftHarbor
{
    ServerProfile profile;
    TextBox profileName,profileVersion,profilePack,profilePackVersion,profileUrl,profileJar,profileArgs,profileJava;
    ComboBox profileClient,profileLaunch,profileSpark;
    NumericUpDown portSetting;
    Label serverIdentity,profileSummary;
    string ProfileFile{get{return Path.Combine(root,"manager-profile.json");}}
    int ServerPort {get{string text;var p=ReadProperties();int value;return p.TryGetValue("server-port",out text)&&int.TryParse(text,out value)&&value>0&&value<=65535?value:25565;}}
    bool SparkAvailable {get{if(profile.SparkMode=="enabled")return true;if(profile.SparkMode=="disabled")return false;foreach(string folder in new[]{"mods","plugins"}){string dir=Path.Combine(ServerDir,folder);if(Directory.Exists(dir)&&Directory.GetFiles(dir,"*spark*.jar").Length>0)return true;}return false;}}
    bool FtbBackupsAvailable {get{return File.Exists(Path.Combine(ServerDir,"config","ftbbackups2.json"));}}
    void LoadProfile()
    {
        profile=new ServerProfile();if(File.Exists(ProfileFile)){try{profile=new JavaScriptSerializer().Deserialize<ServerProfile>(File.ReadAllText(ProfileFile))??new ServerProfile();}catch(Exception e){Add("[プロファイル] "+e.Message);}}
        if(string.IsNullOrWhiteSpace(profile.Name))profile.Name="Minecraft Server";
        profile.MinecraftVersion=profile.MinecraftVersion??"";profile.PackName=profile.PackName??"";profile.PackVersion=profile.PackVersion??"";profile.ClientUrl=profile.ClientUrl??"";profile.ArgumentsFile=profile.ArgumentsFile??"";profile.JarFile=profile.JarFile??"server.jar";profile.ClientMode=profile.ClientMode??"vanilla";profile.LaunchMode=profile.LaunchMode??"jar";profile.SparkMode=profile.SparkMode??"auto";
        if(!File.Exists(ProfileFile)&&!File.Exists(Path.Combine(ServerDir,profile.JarFile))&&Directory.Exists(ServerDir)){var jars=Directory.GetFiles(ServerDir,"*.jar").Where(p=>Path.GetFileName(p).IndexOf("installer",StringComparison.OrdinalIgnoreCase)<0).ToArray();if(jars.Length==1)profile.JarFile=Path.GetFileName(jars[0]);}
    }
    string ProfileMetadata()
    {
        string value="Minecraft Java"+(string.IsNullOrWhiteSpace(profile.MinecraftVersion)?"":" "+profile.MinecraftVersion);
        if(!string.IsNullOrWhiteSpace(profile.PackName))value+=" / "+profile.PackName+(string.IsNullOrWhiteSpace(profile.PackVersion)?"":" "+profile.PackVersion);return value;
    }
    string RelativeServerPath(string value)
    {
        if(string.IsNullOrWhiteSpace(value))throw new Exception("起動ファイルを指定してください。");if(Path.IsPathRooted(value))throw new Exception("起動ファイルはserverフォルダーからの相対パスで指定してください。");string full=Path.GetFullPath(Path.Combine(ServerDir,value));string prefix=Path.GetFullPath(ServerDir)+Path.DirectorySeparatorChar;if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new Exception("起動ファイルはserverフォルダー内に配置してください。");return full;
    }
    string LaunchTarget()
    {
        if(profile.LaunchMode=="argfile"){string p=RelativeServerPath(profile.ArgumentsFile);if(!File.Exists(p))throw new FileNotFoundException("Java引数ファイルがありません。Forge/NeoForgeの導入手順を確認してください。",p);return "@"+Q(p)+" nogui";}
        string jar=RelativeServerPath(profile.JarFile);if(!File.Exists(jar))throw new FileNotFoundException("サーバーJARがありません。サーバー設定の起動ファイルを確認してください。",jar);return "-jar "+Q(jar)+" nogui";
    }
    void AddProfileSettings()
    {
        var form=(FlowLayoutPanel)workspace.TabPages[3].Controls[0];var group=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0,0,0,18)};
        group.Controls.Add(new Label{Text="サーバープロファイル",AutoSize=true,Font=new Font("Yu Gothic UI",13,FontStyle.Bold)});
        profileName=TextSetting(group,"表示名",420);profileName.Text=profile.Name;
        profileVersion=TextSetting(group,"Minecraftバージョン",200);profileVersion.Text=profile.MinecraftVersion;
        profileClient=ChoiceSetting(group,"参加方法",new[]{"vanilla","modpack","custom"});profileClient.SelectedItem=profile.ClientMode;
        profilePack=TextSetting(group,"MODパック名（任意）",420);profilePack.Text=profile.PackName;
        profilePackVersion=TextSetting(group,"パック版（任意）",200);profilePackVersion.Text=profile.PackVersion;
        profileUrl=TextSetting(group,"導入案内URL（任意）",500);profileUrl.Text=profile.ClientUrl;
        profileLaunch=ChoiceSetting(group,"Java起動方式",new[]{"jar","argfile"});profileLaunch.SelectedItem=profile.LaunchMode;
        profileJar=TextSetting(group,"JAR相対パス",420);profileJar.Text=profile.JarFile;
        profileArgs=TextSetting(group,"引数ファイル相対パス",500);profileArgs.Text=profile.ArgumentsFile;
        profileJava=TextSetting(group,"java.exe絶対パス（任意）",500);string javaPath=Path.Combine(root,"java-path.txt");if(File.Exists(javaPath))profileJava.Text=File.ReadAllText(javaPath).Trim();
        profileSpark=ChoiceSetting(group,"spark検出",new[]{"auto","enabled","disabled"});profileSpark.SelectedItem=profile.SparkMode;
        portSetting=NumberSetting(group,"接続ポート",1,65535,ServerPort);
        group.Controls.Add(new Label{Text="jar: Vanilla / Paper / Purpur / Fabric等。argfile: 導入済みForge / NeoForgeのwin_args.txt等。\nJavaの必要バージョンはサーバーに合わせて指定してください。変更は停止中に保存し、次回起動から適用します。",AutoSize=true,MaximumSize=new Size(740,0),ForeColor=Muted});
        var actions=new FlowLayoutPanel{AutoSize=true};FlowBtn(actions,"プロファイルを保存",delegate{SaveProfile();});FlowBtn(actions,"サーバー一覧",delegate{OpenServersDialog();});group.Controls.Add(actions);form.Controls.Add(group);form.Controls.SetChildIndex(group,0);
        Control[] fields={profileName,profileVersion,profileClient,profilePack,profilePackVersion,profileUrl,profileLaunch,profileJar,profileArgs,profileJava,profileSpark,portSetting};string[] labels={"サーバー表示名","Minecraftのバージョン","クライアント参加方法","MODパック名","MODパックのバージョン","参加者向け導入案内URL","Java起動方式","サーバーJARの相対パス","Java引数ファイルの相対パス","使用するjava.exeの絶対パス","spark検出方法","サーバー接続ポート"};for(int i=0;i<fields.Length;i++)fields[i].AccessibleName=labels[i];
        if(!FtbBackupsAvailable){backupHours.Enabled=backupKeep.Enabled=false;foreach(Control c in Descendants(workspace.TabPages[4])){if(c is Button&&(c.Text=="自動設定を保存"||c.Text=="今すぐワールド保存"))c.Enabled=false;if(c is Label&&c.Text.StartsWith("自動保存は同梱"))c.Text="FTB Backupsは未検出です。ワールド自動保存の設定は無効です。下の完全バックアップと復元は利用できます。";}}
    }
    static IEnumerable<Control> Descendants(Control control){foreach(Control child in control.Controls){yield return child;foreach(Control descendant in Descendants(child))yield return descendant;}}
    void SaveProfile()
    {
        EnsureStopped();string name=profileName.Text.Trim();if(name.Length==0||name.Length>80)throw new Exception("表示名は1〜80文字で指定してください。");string url=profileUrl.Text.Trim();Uri u;if(url.Length>0&&(!Uri.TryCreate(url,UriKind.Absolute,out u)||u.Scheme!="https"))throw new Exception("導入案内URLはhttpsのページを指定してください。");
        string java=profileJava.Text.Trim();if(java.Length>0&&(!Path.IsPathRooted(java)||!File.Exists(java)))throw new Exception("java.exeの絶対パスを確認してください。");
        string mode=Convert.ToString(profileLaunch.SelectedItem);string path=mode=="argfile"?profileArgs.Text.Trim():profileJar.Text.Trim();if(!File.Exists(RelativeServerPath(path)))throw new Exception("起動ファイルが見つかりません。");
        var next=new ServerProfile{Name=name,MinecraftVersion=profileVersion.Text.Trim(),ClientMode=Convert.ToString(profileClient.SelectedItem),PackName=profilePack.Text.Trim(),PackVersion=profilePackVersion.Text.Trim(),ClientUrl=url,LaunchMode=mode,JarFile=profileJar.Text.Trim(),ArgumentsFile=profileArgs.Text.Trim(),SparkMode=Convert.ToString(profileSpark.SelectedItem)};
        File.WriteAllText(ProfileFile,new JavaScriptSerializer().Serialize(next),new UTF8Encoding(false));File.WriteAllText(Path.Combine(root,"java-path.txt"),java,new UTF8Encoding(false));string props=Path.Combine(ServerDir,"server.properties"),text=File.ReadAllText(props);text=System.Text.RegularExpressions.Regex.Replace(text,@"(?m)^server-port=.*$","");text=text.TrimEnd()+Environment.NewLine+"server-port="+portSetting.Value+Environment.NewLine;File.WriteAllText(props,text,new UTF8Encoding(false));profile=next;
        RefreshProfileLabels();RefreshGuide();Add("[プロファイル] 保存しました。次回起動に適用されます。");
    }
    void RefreshProfileLabels()
    {
        Text="CraftHarbor — "+profile.Name;if(dashboardTitle!=null){dashboardTitle.Text=workspace.SelectedIndex<=0?profile.Name:PageNames[workspace.SelectedIndex];tips.SetToolTip(dashboardTitle,profile.Name);}if(serverIdentity!=null)serverIdentity.Text=profile.Name;if(profileSummary!=null)profileSummary.Text=ProfileMetadata();if(detail!=null)detail.Text="自分の接続先 localhost:"+ServerPort+"。友達には公開接続先を共有します。";
    }
    void SwitchWorkspace()
    {
        EnsureStopped();using(var dialog=new FolderBrowserDialog{Description="serverフォルダーが入っている別のワークスペースを選択",ShowNewFolderButton=false}){if(dialog.ShowDialog()!=DialogResult.OK)return;if(!ValidWorkspace(dialog.SelectedPath))throw new Exception("有効なサーバーワークスペースではありません。");string selected=dialog.SelectedPath;if(string.Equals(Path.GetFullPath(selected),Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase))return;SaveSettings();StopTunnel();string saved=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CraftHarbor","workspace.txt");Directory.CreateDirectory(Path.GetDirectoryName(saved));File.WriteAllText(saved,selected);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath,"--workspace "+Q(selected)){UseShellExecute=false});quitting=true;Close();}
    }
    string BuildParticipantGuide()
    {
        string endpoint=address.Text.Trim();if(endpoint.Length==0)endpoint="【接続先：発行後ここに貼る】";var s=new StringBuilder();s.AppendLine("🎮 "+profile.Name+" 参加案内").AppendLine();s.AppendLine("**必要なもの**").AppendLine("・Minecraft Java版の正規アカウント").AppendLine("・ゲームのバージョン："+(profile.MinecraftVersion.Length>0?profile.MinecraftVersion:"主催者の指定に合わせてください"));
        if(profile.ClientMode=="modpack"){s.AppendLine("・MODパック："+(profile.PackName.Length>0?profile.PackName:"主催者指定のパック")+(profile.PackVersion.Length>0?" **"+profile.PackVersion+"**":""));s.AppendLine().AppendLine("**導入**").AppendLine("指定のランチャーで上記のMODパックを導入し、ゲーム本体とパック両方のバージョンを揃えて起動してください。").AppendLine("MODの個別追加・更新は主催者に相談してください。");}
        else if(profile.ClientMode=="custom")s.AppendLine().AppendLine("**導入**").AppendLine("必要なMODや設定は、下の導入案内または主催者の指定に合わせてください。");
        else s.AppendLine().AppendLine("**導入**").AppendLine("Minecraft Launcherから指定バージョンのJava版を起動してください。サーバーへの参加用にMODを入れる必要はありません。");
        if(profile.ClientUrl.Length>0)s.AppendLine(profile.ClientUrl);
        s.AppendLine().AppendLine("**接続方法**").AppendLine("「マルチプレイ」→「サーバーを追加」から下のアドレスを入力。").AppendLine("`"+endpoint+"`").AppendLine().AppendLine("**参加前に**").AppendLine("Java版のユーザー名を主催者へ送って、参加許可の設定を確認してください。").AppendLine("接続用のVPNやトンネルアプリは、参加者側では不要です。").AppendLine().AppendLine("**影MOD**").AppendLine("影の設定は各自のPC側で行います。対応するクライアント構成を主催者に確認し、重い場合はOFFにしてください。").AppendLine().AppendLine("**入れないとき**").AppendLine("バージョン、MOD構成、参加許可、アドレス、サーバーの稼働を確認してください。エラーの文面やスクショを主催者へ送ってね。").AppendLine().AppendLine("※サーバー用PCが稼働している間だけ入れます。稼働時間は主催者の案内を確認してください。");return s.ToString();
    }
}
