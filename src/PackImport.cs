using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class PackExtra { public string Url="",Path="",Sha256=""; }
class PackPreset { public string Name="",Url="",Sha256="",MinecraftVersion="",Version="",ClientUrl="";public int JavaMajor=0;public PackExtra[] Extras=new PackExtra[0]; }
class PackProgress { public int Percent;public string Text;public PackProgress(int percent,string text){Percent=percent;Text=text;} }

partial class CraftHarbor
{
    static PackPreset[] PackPresets(){string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"recipes","server-packs.json");return File.Exists(file)?new JavaScriptSerializer().Deserialize<PackPreset[]>(File.ReadAllText(file)):new PackPreset[0];}
    static Uri DownloadUri(string url){Uri u;if(!Uri.TryCreate(url,UriKind.Absolute,out u)||u.Scheme!="https"||u.UserInfo.Length>0||u.Fragment.Length>0)throw new ArgumentException("直接ダウンロードできるHTTPSのサーバー用ZIP URLを指定してください。");return u;}
    static string Sha256File(string path){using(var file=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
    static void DownloadPackFile(string url,string path,string expected,CancellationToken cancel,Action<PackProgress> report)
    {
        Uri uri=DownloadUri(url);ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
        for(int redirect=0;redirect<=6;redirect++){
            cancel.ThrowIfCancellationRequested();var request=(HttpWebRequest)WebRequest.Create(uri);request.AllowAutoRedirect=false;request.UserAgent="CraftHarbor/0.3";request.Timeout=30000;request.ReadWriteTimeout=30000;
            using(cancel.Register(delegate{request.Abort();}))using(var response=(HttpWebResponse)request.GetResponse()){
                int code=(int)response.StatusCode;if(code>=300&&code<400){string location=response.Headers["Location"];if(string.IsNullOrEmpty(location))throw new IOException("配布元の転送先がありません。");uri=DownloadUri(new Uri(uri,location).AbsoluteUri);continue;}
                const long max=8L*1024*1024*1024;if(response.ContentLength>max)throw new IOException("ダウンロードの上限は8 GiBです。");
                Directory.CreateDirectory(Path.GetDirectoryName(path));if(response.ContentLength>0&&new DriveInfo(Path.GetPathRoot(path)).AvailableFreeSpace<response.ContentLength+1073741824L)throw new IOException("ダウンロード用の空き容量が不足しています。");
                using(var input=response.GetResponseStream())using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                    byte[] buffer=new byte[131072];long received=0;int read;DateTime last=DateTime.MinValue;
                    while((read=input.Read(buffer,0,buffer.Length))>0){cancel.ThrowIfCancellationRequested();received+=read;if(received>max)throw new IOException("ダウンロード上限を超えました。");output.Write(buffer,0,read);if((DateTime.UtcNow-last).TotalMilliseconds>150){last=DateTime.UtcNow;report(new PackProgress(response.ContentLength>0?(int)Math.Min(99,received*100/response.ContentLength):-1,"ダウンロード中  "+(received/1048576.0).ToString("0.0")+" MiB"));}}
                }
                cancel.ThrowIfCancellationRequested();if(expected.Length>0&&!string.Equals(Sha256File(path),expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SHA-256が配布元の値と一致しません。取り込みを中止しました。");return;
            }
        }throw new IOException("配布元の転送が多すぎます。直接ダウンロードURLを確認してください。");
    }
    static void ExtractPack(string zip,string target,CancellationToken cancel,Action<PackProgress> report)
    {
        string prefix=Path.GetFullPath(target)+Path.DirectorySeparatorChar;Directory.CreateDirectory(target);
        using(var archive=ZipFile.OpenRead(zip)){
            if(archive.Entries.Count>100000)throw new InvalidDataException("ZIPのファイル数が多すぎます。");long total=0;var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in archive.Entries){cancel.ThrowIfCancellationRequested();string name=entry.FullName.Replace('\\','/');if(name.Length==0)continue;
                foreach(string part in name.TrimEnd('/').Split('/'))if(part.Length==0||part=="."||part==".."||part.IndexOfAny(Path.GetInvalidFileNameChars())>=0||part.EndsWith(".")||part.EndsWith(" ")||Regex.IsMatch(part,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",RegexOptions.IgnoreCase))throw new InvalidDataException("ZIPにWindowsで安全に展開できない名前があります。");
                string dest=Path.GetFullPath(Path.Combine(target,name));if(!dest.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!seen.Add(dest.TrimEnd(Path.DirectorySeparatorChar))||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("ZIPに不正なパス、重複、またはリンクが含まれています。");total=checked(total+entry.Length);if(total>32L*1024*1024*1024)throw new InvalidDataException("展開後の上限は32 GiBです。");
            }
            if(new DriveInfo(Path.GetPathRoot(target)).AvailableFreeSpace<total+1073741824L)throw new IOException("ZIPの展開に必要な空き容量がありません。");int count=0;
            foreach(var entry in archive.Entries){cancel.ThrowIfCancellationRequested();string name=entry.FullName.Replace('\\','/');if(name.Length==0)continue;string dest=Path.Combine(target,name);if(name.EndsWith("/"))Directory.CreateDirectory(dest);else{Directory.CreateDirectory(Path.GetDirectoryName(dest));using(var input=entry.Open())using(var output=new FileStream(dest,FileMode.CreateNew)){byte[] b=new byte[131072];int n;long written=0;while((n=input.Read(b,0,b.Length))>0){cancel.ThrowIfCancellationRequested();written+=n;if(written>entry.Length||written>8L*1024*1024*1024)throw new InvalidDataException("ZIPの実データ長が上限または宣言値を超えました。");output.Write(b,0,n);}if(written!=entry.Length)throw new InvalidDataException("ZIPのデータ長が一致しません。");}}if(++count%40==0)report(new PackProgress(count*100/Math.Max(1,archive.Entries.Count),"ZIPを展開中  "+count+" / "+archive.Entries.Count));}
        }
    }
    static string FindPackRoot(string unpacked)
    {
        Func<string,bool> candidate=delegate(string d){return File.Exists(Path.Combine(d,"server.jar"))||Directory.GetFiles(d,"*.jar").Any(p=>Path.GetFileName(p).IndexOf("installer",StringComparison.OrdinalIgnoreCase)<0)||Directory.Exists(Path.Combine(d,"libraries"));};
        if(candidate(unpacked))return unpacked;var children=Directory.GetDirectories(unpacked).Where(candidate).ToArray();if(children.Length==1)return children[0];throw new InvalidDataException("起動可能なサーバーJAR・librariesが見つかりません。クライアント用ではなく、初期セットアップ済みのサーバー用ZIPを指定してください。");
    }
    static void PreparePack(string unpacked,string workspace,string name,int port,PackPreset preset,string java)
    {
        string server=FindPackRoot(unpacked),target=Path.Combine(workspace,"server");Directory.Move(server,target);
        string properties=Path.Combine(target,"server.properties");string text=File.Exists(properties)?File.ReadAllText(properties):"";
        foreach(string key in new[]{"server-ip","server-port","online-mode","white-list","enforce-whitelist","enable-rcon"})text=Regex.Replace(text,"(?m)^"+key+"=.*(?:\\r?\\n|$)","");
        text=text.TrimEnd()+"\r\nserver-ip=127.0.0.1\r\nserver-port="+port+"\r\nonline-mode=true\r\nwhite-list=true\r\nenforce-whitelist=true\r\nenable-rcon=false\r\n";File.WriteAllText(properties,text,new UTF8Encoding(false));File.WriteAllText(Path.Combine(target,"eula.txt"),"eula=true\r\n",new UTF8Encoding(false));
        var profile=new ServerProfile{Name=name,ClientMode="modpack",PackName=preset==null?name:preset.Name,PackVersion=preset==null?"":preset.Version,MinecraftVersion=preset==null?"":preset.MinecraftVersion,ClientUrl=preset==null?"":preset.ClientUrl};
        string[] jars=Directory.GetFiles(target,"*.jar").Where(p=>Path.GetFileName(p).IndexOf("installer",StringComparison.OrdinalIgnoreCase)<0).ToArray();string jar=jars.FirstOrDefault(p=>Path.GetFileName(p).Equals("server.jar",StringComparison.OrdinalIgnoreCase))??jars.FirstOrDefault(p=>Path.GetFileName(p).IndexOf("fabric",StringComparison.OrdinalIgnoreCase)>=0)??(jars.Length==1?jars[0]:null);
        string args=Directory.Exists(Path.Combine(target,"libraries"))?Directory.GetFiles(Path.Combine(target,"libraries"),"win_args.txt",SearchOption.AllDirectories).FirstOrDefault():null;
        if(jar!=null)profile.JarFile=jar.Substring(target.Length+1);else if(args!=null){profile.LaunchMode="argfile";profile.ArgumentsFile=args.Substring(target.Length+1);}else throw new InvalidDataException("起動ファイルを一意に判別できません。サーバーパックを初期セットアップしてから登録してください。");
        File.WriteAllText(Path.Combine(workspace,"manager-profile.json"),new JavaScriptSerializer().Serialize(profile),new UTF8Encoding(false));if(java.Length>0)File.WriteAllText(Path.Combine(workspace,"java-path.txt"),java,new UTF8Encoding(false));
        if(!File.Exists(Path.Combine(target,"whitelist.json")))File.WriteAllText(Path.Combine(target,"whitelist.json"),"[]");
    }
    static string ImportPackUrl(string url,string destination,string name,int port,string sha,string java,CancellationToken cancel,Action<PackProgress> report)
    {
        DownloadUri(url);if(name.Length==0||name.Length>80)throw new ArgumentException("サーバー名を1〜80文字で指定してください。");if(sha.Length>0&&!Regex.IsMatch(sha,"^[a-fA-F0-9]{64}$"))throw new ArgumentException("SHA-256は64桁の16進数で指定してください。");
        destination=Path.GetFullPath(destination);if(Directory.Exists(destination)||File.Exists(destination))throw new IOException("保存先は新しいフォルダー名を指定してください。既存のデータは上書きしません。");string parent=Path.GetDirectoryName(destination);Directory.CreateDirectory(parent);string stage=Path.Combine(parent,".craftharbor-import-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        try{var preset=PackPresets().FirstOrDefault(p=>p.Url==url);if(preset!=null){if(sha.Length>0&&!sha.Equals(preset.Sha256,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("選択した配布URLのSHA-256を確認してください。");sha=preset.Sha256;}
            string zip=Path.Combine(stage,"download.zip"),unpacked=Path.Combine(stage,"unpacked"),workspace=Path.Combine(stage,"workspace");DownloadPackFile(url,zip,sha,cancel,report);report(new PackProgress(-1,"アーカイブを検証しています"));ExtractPack(zip,unpacked,cancel,report);Directory.CreateDirectory(workspace);PreparePack(unpacked,workspace,name,port,preset,java);
            if(preset!=null)foreach(var extra in preset.Extras){cancel.ThrowIfCancellationRequested();string dest=Path.GetFullPath(Path.Combine(workspace,"server",extra.Path));string prefix=Path.GetFullPath(Path.Combine(workspace,"server"))+Path.DirectorySeparatorChar;if(!dest.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("追加ファイルの保存先が不正です。");if(!File.Exists(dest)){report(new PackProgress(-1,"パックに必要な追加ファイルを取得中"));DownloadPackFile(extra.Url,dest,extra.Sha256,cancel,report);}else if(!Sha256File(dest).Equals(extra.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("パックの追加ファイルが既定の値と一致しません。");}
            File.WriteAllText(Path.Combine(workspace,"import-info.json"),new JavaScriptSerializer().Serialize(new{sourceHost=new Uri(url).Host,sha256=Sha256File(zip),importedUtc=DateTime.UtcNow.ToString("o")}),new UTF8Encoding(false));cancel.ThrowIfCancellationRequested();Directory.Move(workspace,destination);report(new PackProgress(100,"取り込み完了。参加者の許可とJava・起動設定を確認してから起動してください。"));return destination;
        }finally{try{string full=Path.GetFullPath(stage);if(string.Equals(Path.GetDirectoryName(full),parent,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(full).StartsWith(".craftharbor-import-",StringComparison.Ordinal)&&Directory.Exists(full))Directory.Delete(full,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
    static string ShowPackImport(IWin32Window owner,string baseDirectory,string java,bool snapshot=false)
    {
        string result=null;using(var dialog=new HarborDialog{Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath),Text="URLからサーバーパックを追加",Size=new Size(900,760),MinimumSize=new Size(780,700),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=new Font("Yu Gothic UI",10)}){
            var form=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(24),AutoScroll=true};dialog.Controls.Add(form);
            form.Controls.Add(new Label{Text="新しいサーバーを、配布URLから。",AutoSize=true,Font=new Font("Yu Gothic UI",20,FontStyle.Bold)});
            form.Controls.Add(new Label{Text="直接ダウンロードできるサーバー用ZIPに対応。展開だけを行い、起動スクリプトは実行しません。",AutoSize=true,ForeColor=Muted,MaximumSize=new Size(800,0),Margin=new Padding(0,8,0,18)});
            var presets=PackPresets();var choose=ChoiceSetting(form,"入力例",new[]{"カスタムURL"}.Concat(presets.Select(p=>p.Name+" "+p.Version)).ToArray());choose.Width=540;choose.SelectedIndex=0;
            var name=TextSetting(form,"サーバー名",520);name.Text="My Minecraft Server";var url=TextSetting(form,"サーバーZIP URL",540);var hash=TextSetting(form,"SHA-256（任意）",540);var destination=TextSetting(form,"新しい保存先",540);destination.Text=Path.Combine(baseDirectory,"my-server");
            var browse=new ActionButton{Text="保存する親フォルダーを選択",BackColor=Card,ForeColor=Ink,Size=new Size(280,42)};form.Controls.Add(browse);browse.Click+=delegate{using(var folder=new FolderBrowserDialog{Description="新しいサーバーの親フォルダーを選択"})if(folder.ShowDialog(dialog)==DialogResult.OK)destination.Text=Path.Combine(folder.SelectedPath,Regex.Replace(name.Text,"[^A-Za-z0-9_-]","-").Trim('-'));};browse.Width=280;
            var javaInput=TextSetting(form,"java.exe（任意）",540);javaInput.Text=java;var port=NumberSetting(form,"接続ポート",1,65535,25566);
            form.Controls.Add(new Label{Text="登録後にサーバー設定から起動方法・Java・参加許可を調整できます。Create: Astralの例はJava 17を使用し、既知の不足MODを配布元からSHA-256検証付きで補完します。",ForeColor=Muted,AutoSize=true,MaximumSize=new Size(790,0),Margin=new Padding(0,12,0,12)});
            var eula=new HarborCheckBox{Text="Minecraft EULAを確認し、このサーバーへの適用に同意します",AutoSize=true};form.Controls.Add(eula);var link=new LinkLabel{Text="Minecraft EULAを開く",AutoSize=true,LinkColor=Accent};link.LinkClicked+=delegate{System.Diagnostics.Process.Start("https://www.minecraft.net/eula");};form.Controls.Add(link);
            var progress=new HarborProgress{Width=780,Height=8};form.Controls.Add(progress);var status=new Label{Text="URLと新しい保存先を指定してください。",AutoSize=true,MaximumSize=new Size(780,0),ForeColor=Muted,AccessibleName="サーバーパック取り込みの状態"};form.Controls.Add(status);
            var actions=new FlowLayoutPanel{AutoSize=true,Margin=new Padding(0,16,0,0)};form.Controls.Add(actions);var run=new ActionButton{Text="ダウンロードして追加",Size=new Size(230,44),BackColor=Card,ForeColor=Ink,Primary=true,IconKey="mods"};actions.Controls.Add(run);var cancelButton=new ActionButton{Text="閉じる",Size=new Size(120,44),BackColor=Card,ForeColor=Ink,Margin=new Padding(12,0,0,0)};actions.Controls.Add(cancelButton);CancellationTokenSource cancellation=null;bool working=false;
            choose.SelectedIndexChanged+=delegate{if(choose.SelectedIndex>0){var p=presets[choose.SelectedIndex-1];name.Text=p.Name+" "+p.Version;url.Text=p.Url;hash.Text=p.Sha256;destination.Text=Path.Combine(baseDirectory,"create-astral-"+p.Version);}};
            cancelButton.Click+=delegate{if(working){cancellation.Cancel();status.Text="中止しています…";}else dialog.Close();};dialog.FormClosing+=delegate(object s,FormClosingEventArgs e){if(working){e.Cancel=true;cancellation.Cancel();}};
            run.Click+=async delegate{if(!eula.Checked){status.Text="EULAを確認して同意にチェックしてください。";return;}try{DownloadUri(url.Text.Trim());string download=url.Text.Trim(),target=destination.Text.Trim(),serverName=name.Text.Trim(),expected=hash.Text.Trim(),javaPath=javaInput.Text.Trim();int serverPort=(int)port.Value;if(javaPath.Length>0&&(!Path.IsPathRooted(javaPath)||!File.Exists(javaPath)))throw new ArgumentException("java.exeの絶対パスを確認してください。");cancellation=new CancellationTokenSource();working=true;foreach(Control c in new Control[]{run,choose,name,url,hash,destination,browse,javaInput,port,eula})c.Enabled=false;cancelButton.Text="中止";
                var reporter=new Progress<PackProgress>(p=>{if(dialog.IsDisposed)return;progress.Style=p.Percent<0?ProgressBarStyle.Marquee:ProgressBarStyle.Continuous;if(p.Percent>=0)progress.Value=Math.Max(0,Math.Min(100,p.Percent));status.Text=p.Text;});result=await Task.Run(()=>ImportPackUrl(download,target,serverName,serverPort,expected,javaPath,cancellation.Token,p=>((IProgress<PackProgress>)reporter).Report(p)));working=false;dialog.DialogResult=DialogResult.OK;dialog.Close();
            }catch(Exception e){status.Text=cancellation!=null&&cancellation.IsCancellationRequested?"中止しました。保存先には追加していません。":e.Message;working=false;foreach(Control c in new Control[]{run,choose,name,url,hash,destination,browse,javaInput,port,eula})c.Enabled=true;cancelButton.Text="閉じる";progress.Style=ProgressBarStyle.Continuous;progress.Value=0;}finally{if(cancellation!=null)cancellation.Dispose();}};
            var footer=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=244,ColumnCount=1,RowCount=5,Padding=new Padding(24,8,24,12),BackColor=Bg};foreach(float height in new[]{38f,32f,8f,74f})footer.RowStyles.Add(new RowStyle(SizeType.Absolute,height));footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));Control[] footerControls={eula,link,progress,status,actions};for(int i=0;i<footerControls.Length;i++){var c=footerControls[i];form.Controls.Remove(c);c.Margin=new Padding(0);c.Dock=DockStyle.Fill;footer.Controls.Add(c,0,i);}dialog.Controls.Add(footer);dialog.AcceptButton=run;dialog.CancelButton=cancelButton;
            StyleImportInputs(form);var managerOwner=owner as CraftHarbor;if(managerOwner!=null)managerOwner.ThemeDialog(dialog);
            if(snapshot){choose.SelectedIndex=presets.Length>0?1:0;dialog.Shown+=delegate{dialog.BeginInvoke((Action)delegate{using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(baseDirectory,"ui-import.png"),System.Drawing.Imaging.ImageFormat.Png);}dialog.Close();});};}
            dialog.ShowDialog(owner);
        }return result;
    }
    static void StyleImportInputs(Control root){foreach(Control c in root.Controls.Cast<Control>().ToArray())if(c is TextBox||c is NumericUpDown||c is ComboBox)InputFrame.Wrap(c);else StyleImportInputs(c);}
    void ImportServerPack(){string result=ShowPackImport(this,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CraftHarbor","servers"),JavaPath());if(result!=null){RegisterWorkspace(result);memoryPreviewAt=DateTime.MinValue;Add("[追加] サーバーパックを取り込みました。サーバー一覧から開けます。");OpenRegistered(result,false);}}
}
