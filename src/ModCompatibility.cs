using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class ParticipantMod
{
    public string Id="",Version="",Role="unknown",Sha256="",DistributionUrl="";
}
class ParticipantManifest
{
    public int FormatVersion=1;
    public string MinecraftVersion="",PackName="",PackVersion="";
    public List<ParticipantMod> Mods=new List<ParticipantMod>();
}

partial class CraftHarbor
{
    static readonly string[] ModRoles={"unknown","required","server-only","client-only"};
    static string ModDistributionUrl(string text)
    {
        if(string.IsNullOrWhiteSpace(text))return "";Uri uri;
        if(text.Length>2048||!Uri.TryCreate(text,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.UserInfo.Length!=0||!uri.IsDefaultPort||uri.Query.Length!=0||uri.Fragment.Length!=0)throw new InvalidDataException("配布先は認証情報やqueryのないHTTPS URLで指定してください。");
        // These links are supplied by the MOD metadata or the host, not looked up or endorsed automatically.
        if(!((uri.Host=="modrinth.com"&&uri.AbsolutePath.StartsWith("/mod/"))||((uri.Host=="www.curseforge.com"||uri.Host=="curseforge.com")&&uri.AbsolutePath.StartsWith("/minecraft/mc-mods/"))||(uri.Host=="github.com"&&uri.AbsolutePath.Trim('/').Split('/').Length>=2)))throw new InvalidDataException("正規配布元を確認し、Modrinth/CurseForgeのMODページまたは公式GitHubを指定してください。");
        return uri.AbsoluteUri;
    }
    static string SafeModLink(string text){try{return ModDistributionUrl(text);}catch(InvalidDataException){return "";}}
    static string ReadModMetadata(ZipArchive archive,string name)
    {
        var matches=archive.Entries.Where(e=>e.FullName==name).ToArray();if(matches.Length>1)throw new InvalidDataException("同名メタデータが重複しています。");if(matches.Length==0)return "";var entry=matches[0];const int limit=1048576;if(entry.Length>limit)throw new InvalidDataException("MODメタデータが大きすぎます。");
        using(var input=entry.Open())using(var output=new MemoryStream()){var buffer=new byte[4096];int count;while((count=input.Read(buffer,0,buffer.Length))>0){if(output.Length+count>limit)throw new InvalidDataException("MODメタデータが大きすぎます。");output.Write(buffer,0,count);}return new UTF8Encoding(false,true).GetString(output.ToArray());}
    }
    static string ModTomlValue(string text,string name)
    {
        var match=Regex.Match(text,@"(?m)^\s*"+Regex.Escape(name)+"\\s*=\\s*[\"']([^\"'\\r\\n]*)[\"']\\s*(?:#.*)?$");return match.Success?match.Groups[1].Value:"";
    }
    static List<ParticipantMod> ParseParticipantMods(ZipArchive archive,string sha)
    {
        var result=new List<ParticipantMod>();string fabric=ReadModMetadata(archive,"fabric.mod.json");
        if(fabric.Length>0){
            var json=new JavaScriptSerializer{MaxJsonLength=1048576,RecursionLimit=32}.Deserialize<Dictionary<string,object>>(fabric);if(json==null)throw new InvalidDataException("MODメタデータが空です。");string environment=StringValue(json,"environment");object contactObject;var contact=json.TryGetValue("contact",out contactObject)?contactObject as Dictionary<string,object>:null;
            result.Add(new ParticipantMod{Id=StringValue(json,"id"),Version=StringValue(json,"version"),Role=environment=="client"?"client-only":environment=="server"?"server-only":"unknown",Sha256=sha,DistributionUrl=contact==null?"":SafeModLink(StringValue(contact,"homepage"))});
        }else{
            string toml=ReadModMetadata(archive,"META-INF/neoforge.mods.toml");if(toml.Length==0)toml=ReadModMetadata(archive,"META-INF/mods.toml");
            // Parse only ordinary scalar entries inside [[mods]]. TOML interpolation and complex syntax remain unknown.
            foreach(Match section in Regex.Matches(toml,@"(?ms)^\s*\[\[mods\]\]\s*\r?\n(.*?)(?=^\s*\[|\z)")){
                string id=ModTomlValue(section.Groups[1].Value,"modId"),version=ModTomlValue(section.Groups[1].Value,"version");if(version.Contains("${"))version="";
                result.Add(new ParticipantMod{Id=id,Version=version,Role="unknown",Sha256=sha,DistributionUrl=SafeModLink(ModTomlValue(section.Groups[1].Value,"displayURL"))});
            }
        }
        if(result.Count==0)result.Add(new ParticipantMod{Id="unknown_"+sha.Substring(0,16),Sha256=sha});
        foreach(var mod in result){if(!Regex.IsMatch(mod.Id??"",@"^[a-zA-Z0-9][a-zA-Z0-9_.-]{1,127}$"))throw new InvalidDataException("MOD IDを確認できません。");if(mod.Version.Length>256||mod.Version.Any(char.IsControl))mod.Version="";}
        return result;
    }
    static ParticipantManifest ScanParticipantMods(string folder,CancellationToken cancel=default(CancellationToken))
    {
        cancel.ThrowIfCancellationRequested();if(!Directory.Exists(folder))throw new DirectoryNotFoundException("modsフォルダーを指定してください。");if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("リンク先のMODフォルダーは対象外です。");
        string[] paths=Directory.GetFiles(folder,"*.jar");if(paths.Length>2000)throw new InvalidDataException("一度に照合するMODは2000ファイルまでです。");var manifest=new ParticipantManifest();
        foreach(string path in paths){cancel.ThrowIfCancellationRequested();if(new FileInfo(path).Length>536870912L)throw new InvalidDataException("512 MiBを超えるJARは自動照合の対象外です。");if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("リンク先のJARは対象外です。");
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
                string sha;using(var hash=SHA256.Create())sha=BitConverter.ToString(hash.ComputeHash(file)).Replace("-","").ToLowerInvariant();file.Position=0;cancel.ThrowIfCancellationRequested();
                try{using(var archive=new ZipArchive(file,ZipArchiveMode.Read,true))manifest.Mods.AddRange(ParseParticipantMods(archive,sha));}
                catch(InvalidDataException){manifest.Mods.Add(new ParticipantMod{Id="unknown_"+sha.Substring(0,16),Sha256=sha});}
                catch(ArgumentException){manifest.Mods.Add(new ParticipantMod{Id="unknown_"+sha.Substring(0,16),Sha256=sha});}
            }
        }return ValidateParticipantManifest(manifest);
    }
    static ParticipantManifest ValidateParticipantManifest(ParticipantManifest manifest)
    {
        if(manifest==null||manifest.FormatVersion!=1||manifest.Mods==null||manifest.Mods.Count>4000)throw new InvalidDataException("対応する構成リストではありません。");
        foreach(string text in new[]{manifest.MinecraftVersion,manifest.PackName,manifest.PackVersion})if(text==null||text.Length>256||text.Any(char.IsControl))throw new InvalidDataException("構成リストの版情報が不正です。");
        foreach(var mod in manifest.Mods){if(mod==null||!Regex.IsMatch(mod.Id??"",@"^[a-zA-Z0-9][a-zA-Z0-9_.-]{1,127}$")||mod.Version==null||mod.Version.Length>256||mod.Version.Any(char.IsControl)||!ModRoles.Contains(mod.Role)||!Regex.IsMatch(mod.Sha256??"",@"^[a-f0-9]{64}$"))throw new InvalidDataException("MODのID・版・役割・ハッシュを確認してください。");mod.DistributionUrl=ModDistributionUrl(mod.DistributionUrl);}
        return manifest;
    }
    static ParticipantManifest ReadParticipantManifest(string path)
    {
        if(new FileInfo(path).Length>4194304)throw new InvalidDataException("構成リストが大きすぎます。");return ValidateParticipantManifest(new JavaScriptSerializer{MaxJsonLength=4194304,RecursionLimit=32}.Deserialize<ParticipantManifest>(File.ReadAllText(path)));
    }
    static string CompareParticipantMods(ParticipantManifest expected,ParticipantManifest participant)
    {
        ValidateParticipantManifest(expected);ValidateParticipantManifest(participant);var text=new StringBuilder("参加者MOD照合\r\nメタデータ・版文字列の比較です。依存関係、ローダー、設定、通信互換性は保証しません。JAR・設定・ワールドは共有しません。\r\n");int missing=0,versions=0,unknown=0;
        if(expected.MinecraftVersion.Length>0&&participant.MinecraftVersion.Length>0&&expected.MinecraftVersion!=participant.MinecraftVersion)text.AppendLine("ゲーム版が異なります: "+expected.MinecraftVersion+" / "+participant.MinecraftVersion);
        var expectedGroups=expected.Mods.GroupBy(m=>m.Id,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.OrdinalIgnoreCase);var actualGroups=participant.Mods.GroupBy(m=>m.Id,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.OrdinalIgnoreCase);
        foreach(var pair in expectedGroups){var mods=pair.Value;ParticipantMod[] actual;actualGroups.TryGetValue(pair.Key,out actual);var mod=mods[0];string state;
            if(mods.Length!=1||(actual!=null&&actual.Length!=1)||mod.Id.StartsWith("unknown_")){state="判定不可（ID不明 / 重複）";unknown++;}
            else if(mod.Role=="server-only")state="サーバー専用（参加者への導入要求なし）";
            else if(mod.Role=="client-only")state=actual==null?"クライアント専用（任意・要確認）":"クライアント専用（検出）";
            else if(mod.Role=="unknown"){state="必要性不明（主催者が役割を確認）";unknown++;if(actual==null)state+="・参加者側なし";else if(mod.Version!=actual[0].Version)state+="・版も要確認";}
            else if(actual==null){state="不足（主催者がrequired指定）";missing++;}
            else if(mod.Version.Length==0||actual[0].Version.Length==0){state="版不明";unknown++;}
            else if(mod.Version!=actual[0].Version){state="版違い: 必要 "+mod.Version+" / 参加者 "+actual[0].Version;versions++;}
            else state=mod.Sha256==actual[0].Sha256?"版・ハッシュ一致（互換性は別途確認）":"版一致・内容差分あり（互換性は要確認）";
            text.AppendLine().AppendLine(mod.Id+" — "+state);if(mod.DistributionUrl.Length>0)text.AppendLine("主催者/メタデータ指定の配布先（正規性を確認）: "+mod.DistributionUrl);
        }
        foreach(var pair in actualGroups.Where(p=>!expectedGroups.ContainsKey(p.Key)))text.AppendLine().AppendLine(pair.Key+" — 参加者側の追加MOD"+(pair.Value.Length==1&&pair.Value[0].Role=="client-only"?"（クライアント専用）":"（必要性・互換性は未確認）"));
        return "不足 "+missing+" / 版違い "+versions+" / 判定保留 "+unknown+"\r\n\r\n"+text;
    }
    void ShowModCompatibility(bool snapshot=false)
    {
        using(var lifetime=new CancellationTokenSource())
        using(var dialog=new Form{Text="参加者MOD照合 / 構成リスト",Size=new Size(980,820),MinimumSize=new Size(720,560),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=Font}){
            var cancel=lifetime.Token;dialog.FormClosing+=delegate{lifetime.Cancel();};
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),RowCount=4,ColumnCount=1};layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,48));layout.RowStyles.Add(new RowStyle(SizeType.Percent,52));dialog.Controls.Add(layout);
            layout.Controls.Add(new Label{Dock=DockStyle.Fill,AutoSize=true,MaximumSize=new Size(900,0),Text="サーバーMODを読み、役割と正規配布先を確認して構成リストを書き出します。参加者は自身のmodsフォルダーまたは同形式のJSONで照合できます。\r\nunknownは必須扱いしません。両側で動くMODでも参加者に必須とは限らないため、requiredは主催者が明示します。JAR・秘密設定は書き出しません。",ForeColor=Muted},0,0);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};layout.Controls.Add(actions,0,1);var grid=Grid();grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize;grid.ReadOnly=false;grid.AllowUserToAddRows=false;grid.Columns.Add(new DataGridViewTextBoxColumn{Name="id",HeaderText="MOD ID",ReadOnly=true});grid.Columns.Add(new DataGridViewTextBoxColumn{Name="version",HeaderText="版",ReadOnly=true});grid.Columns.Add(new DataGridViewComboBoxColumn{Name="role",HeaderText="役割",DataSource=ModRoles});grid.Columns.Add(new DataGridViewTextBoxColumn{Name="url",HeaderText="正規配布先（確認して入力）"});layout.Controls.Add(grid,0,2);
            var report=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Card,ForeColor=Ink,Text="まずサーバーMODを読むか、主催者の構成JSONを読み込んでください。",AccessibleName="MOD照合結果と保留事項"};layout.Controls.Add(report,0,3);ParticipantManifest selected=null;bool busy=false;
            Action<ParticipantManifest> populate=delegate(ParticipantManifest manifest){selected=manifest;grid.Rows.Clear();foreach(var mod in manifest.Mods){int index=grid.Rows.Add(mod.Id,mod.Version,mod.Role,mod.DistributionUrl);grid.Rows[index].Tag=mod;}report.Text="読み込み "+manifest.Mods.Count+" MOD。役割と配布先を確認してください。ローダーや依存関係、jar内の入れ子MODは完全解析しません。";};
            Action apply=delegate{if(selected==null)throw new InvalidOperationException("先に構成を読み込んでください。");grid.EndEdit();foreach(DataGridViewRow row in grid.Rows){var mod=row.Tag as ParticipantMod;if(mod==null||!selected.Mods.Contains(mod))throw new InvalidDataException("MOD行の対応を確認できません。");mod.Role=Convert.ToString(row.Cells["role"].Value);mod.DistributionUrl=Convert.ToString(row.Cells["url"].Value);}ValidateParticipantManifest(selected);};
            FlowBtn(actions,"サーバーMODを読む",async delegate{if(busy)return;busy=true;try{var manifest=await Task.Run(()=>Directory.Exists(Path.Combine(ServerDir,"mods"))?ScanParticipantMods(Path.Combine(ServerDir,"mods"),cancel):new ParticipantManifest());manifest.MinecraftVersion=profile.MinecraftVersion;manifest.PackName=profile.PackName;manifest.PackVersion=profile.PackVersion;if(!dialog.IsDisposed)populate(manifest);}catch(Exception e){if(!dialog.IsDisposed)report.Text=e.Message;}finally{busy=false;}});
            FlowBtn(actions,"構成JSONを開く",delegate{if(busy)return;using(var file=new OpenFileDialog{Filter="CraftHarbor構成JSON|*.json"})if(file.ShowDialog(dialog)==DialogResult.OK)try{populate(ReadParticipantManifest(file.FileName));}catch(Exception e){report.Text=e.Message;}});
            FlowBtn(actions,"構成JSONを書き出す",delegate{if(busy)return;try{apply();using(var file=new SaveFileDialog{Filter="CraftHarbor構成JSON|*.json",FileName="craftharbor-participant-mods.json"})if(file.ShowDialog(dialog)==DialogResult.OK){File.WriteAllText(file.FileName,new JavaScriptSerializer{MaxJsonLength=4194304}.Serialize(selected),new UTF8Encoding(false));report.Text="構成JSONだけを保存しました。JAR・設定・ファイルパス・参加者名は含みません。配布先の正規性と必要版を確認して共有してください。";}}catch(Exception e){report.Text=e.Message;}});
            FlowBtn(actions,"参加者modsと照合",async delegate{if(busy)return;try{apply();using(var folder=new FolderBrowserDialog{Description="参加者のmodsフォルダーを選択（読み取り専用）"}){if(folder.ShowDialog(dialog)!=DialogResult.OK)return;busy=true;string path=folder.SelectedPath;var actual=await Task.Run(()=>ScanParticipantMods(path,cancel));if(!dialog.IsDisposed)report.Text=CompareParticipantMods(selected,actual);}}catch(Exception e){if(!dialog.IsDisposed)report.Text=e.Message;}finally{busy=false;}});
            FlowBtn(actions,"参加者JSONと照合",delegate{if(busy)return;try{apply();using(var file=new OpenFileDialog{Filter="CraftHarbor構成JSON|*.json"})if(file.ShowDialog(dialog)==DialogResult.OK)report.Text=CompareParticipantMods(selected,ReadParticipantManifest(file.FileName));}catch(Exception e){report.Text=e.Message;}});FlowBtn(actions,"閉じる",delegate{dialog.Close();});StyleControls(dialog);ThemeDialog(dialog);if(snapshot)CaptureOperationsDialog(dialog,"ui-mod-compatibility.png",delegate{
                string hash=new string('a',64);populate(new ParticipantManifest{Mods=new List<ParticipantMod>{new ParticipantMod{Id="a_required",Version="1.0",Role="required",Sha256=hash},new ParticipantMod{Id="z_server",Version="2.0",Sha256=hash}}});grid.Sort(grid.Columns["id"],System.ComponentModel.ListSortDirection.Descending);grid.Rows[0].Cells["role"].Value="server-only";apply();Require(selected.Mods.Single(m=>m.Id=="z_server").Role=="server-only"&&selected.Mods.Single(m=>m.Id=="a_required").Role=="required","sorted MOD rows retain role identity");report.Text=CompareParticipantMods(selected,new ParticipantManifest());
            });dialog.ShowDialog(this);
        }
    }
    static void RunModCompatibilityChecks()
    {
        string sha=new string('a',64);var server=new ParticipantManifest{Mods=new List<ParticipantMod>{new ParticipantMod{Id="required_mod",Version="1.0",Role="required",Sha256=sha},new ParticipantMod{Id="server_mod",Version="1",Role="server-only",Sha256=sha},new ParticipantMod{Id="uncertain_mod",Version="1",Sha256=sha}}};
        string missing=CompareParticipantMods(server,new ParticipantManifest());Require(missing.Contains("不足 1 / 版違い 0 / 判定保留 1")&&missing.Contains("サーバー専用"),"required missing differs from server-only and unknown");
        var participant=new ParticipantManifest{Mods=new List<ParticipantMod>{new ParticipantMod{Id="required_mod",Version="2.0",Sha256=sha},new ParticipantMod{Id="visual_mod",Version="1",Role="client-only",Sha256=sha}}};string result=CompareParticipantMods(server,participant);Require(result.Contains("不足 0 / 版違い 1")&&result.Contains("参加者側の追加MOD（クライアント専用）"),"version mismatch and client-only extra");
        participant.Mods.Add(participant.Mods[0]);Require(CompareParticipantMods(server,participant).Contains("ID不明 / 重複"),"duplicate IDs never silently selected");
        foreach(string bad in new[]{"https://user:pass@modrinth.com/mod/test","https://modrinth.com/mod/test?token=secret","http://github.com/org/repo","https://example.invalid/file.jar"}){bool rejected=false;try{ModDistributionUrl(bad);}catch(InvalidDataException){rejected=true;}Require(rejected,"unsafe/unverified distribution URLs rejected");}
        using(var memory=new MemoryStream()){
            using(var archive=new ZipArchive(memory,ZipArchiveMode.Create,true)){using(var writer=new StreamWriter(archive.CreateEntry("fabric.mod.json").Open()))writer.Write("{\"id\":\"visual_mod\",\"version\":\"1.2\",\"environment\":\"client\",\"contact\":{\"homepage\":\"https://modrinth.com/mod/test\"}}");}
            memory.Position=0;using(var archive=new ZipArchive(memory,ZipArchiveMode.Read)){var mods=ParseParticipantMods(archive,sha);Require(mods.Count==1&&mods[0].Role=="client-only"&&mods[0].Version=="1.2"&&mods[0].DistributionUrl.Length>0,"Fabric metadata and side parsed without executing JAR");}
        }
        using(var memory=new MemoryStream()){
            using(var archive=new ZipArchive(memory,ZipArchiveMode.Create,true)){using(var writer=new StreamWriter(archive.CreateEntry("META-INF/mods.toml").Open()))writer.Write("modLoader=\"javafml\"\n[[mods]]\nmodId=\"first_mod\"\nversion=\"1.0\"\n[[mods]]\nmodId='second_mod'\nversion='${file.jarVersion}'\n");}
            memory.Position=0;using(var archive=new ZipArchive(memory,ZipArchiveMode.Read)){var mods=ParseParticipantMods(archive,sha);Require(mods.Count==2&&mods[0].Id=="first_mod"&&mods[1].Version==""&&mods.All(m=>m.Role=="unknown"),"multiple Forge mods parsed; unresolved version and required-side stay unknown");}
        }
        string exported=new JavaScriptSerializer().Serialize(server);Require(!exported.Contains("FileName")&&!exported.Contains("config")&&!exported.Contains("Directory"),"manifest has no JAR payload, paths or config");
    }
}
