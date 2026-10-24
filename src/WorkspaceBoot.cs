using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

partial class CraftHarbor
{
    static string WorkspaceRoot=AppDomain.CurrentDomain.BaseDirectory;
    static bool ValidWorkspace(string path)
    {
        try{string server=Path.Combine(path,"server");return Directory.Exists(server)&&File.Exists(Path.Combine(server,"server.properties"))&&(Directory.GetFiles(server,"*.jar").Length>0||Directory.Exists(Path.Combine(server,"libraries")));}catch(ArgumentException){return false;}catch(IOException){return false;}
    }
    static bool InitializeWorkspace(string[] args)
    {
        try{
            if(Array.IndexOf(args,"--experience-check")<0)MigrateLegacySettings();
            for(int i=0;i<args.Length-1;i++)if(args[i]=="--workspace"){
                string path=Path.GetFullPath(args[i+1]);if(!ValidWorkspace(path))throw new Exception("ワークスペースには server/server.properties と導入済みのJavaサーバーが必要です。");WorkspaceRoot=path;return true;
            }
            if(ValidWorkspace(WorkspaceRoot))return true;
            string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CraftHarbor","workspace.txt");
            if(File.Exists(settings)){string saved=File.ReadAllText(settings).Trim();if(ValidWorkspace(saved)){WorkspaceRoot=saved;return true;}}
            Application.EnableVisualStyles();
            MessageBox.Show("導入済みのMinecraft Javaサーバーを『server』フォルダーへ配置し、その親フォルダーを選択してください。\n\nVanilla、Paper、FabricやMODパック等を管理できます。Minecraft・Java・MOD・playitはこの管理アプリに含まれません。","サーバーの保存先を選択");
            using(var dialog=new FolderBrowserDialog{Description="serverフォルダーが入っているワークスペースを選択",ShowNewFolderButton=false}){
                if(dialog.ShowDialog()!=DialogResult.OK)return false;
                if(!ValidWorkspace(dialog.SelectedPath))throw new Exception("serverフォルダーが見つかりません。READMEの構成に合わせて、もう一度起動してください。");
                WorkspaceRoot=dialog.SelectedPath;Directory.CreateDirectory(Path.GetDirectoryName(settings));File.WriteAllText(settings,WorkspaceRoot,new UTF8Encoding(false));return true;
            }
        }catch(Exception e){MessageBox.Show(e.Message,"ワークスペースを開けませんでした");return false;}
    }
    static void MigrateLegacySettings()
    {
        string legacy=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AstralServerManager");
        if(!Directory.Exists(legacy))return;
        Directory.CreateDirectory(ManagerData);
        foreach(string name in new[]{"workspace.txt","servers.json"}){
            string source=Path.Combine(legacy,name),target=Path.Combine(ManagerData,name);
            if(File.Exists(source)&&!File.Exists(target))try{File.Copy(source,target,false);}catch(IOException){if(!File.Exists(target))throw;}
        }
    }
    static void Require(bool condition,string reason){if(!condition)throw new Exception(reason);}
    static int CoreChecks()
    {
        string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"core-check-report.txt");
        try{
            Require(AutoHeap(64,60)==57,"64GiB high-availability heap policy");Require(AutoHeap(64,30)==27,"64GiB constrained heap policy");Require(AutoHeap(8,3)<4,"low memory rejected");Require(AutoHeap(4,1)==0,"very low memory rejected");
            Require(SharedBudget(64,60,2,0,0)==29,"two-server share");Require(SharedBudget(64,55,2,29,25)==27,"other JVM reserved growth protected");Require(SharedBudget(64,55,2,57,53)<4,"no overcommit after a full-size server is running");Require(WorkspaceMutexName("C:\\Servers\\One")==WorkspaceMutexName("c:\\servers\\one\\"),"workspace mutex canonicalization");
            string url="https://discord.com/api/webhooks/"+new string('1',18)+"/format_check_only";Require(ValidateWebhook(url)==url,"official webhook accepted");
            foreach(string invalid in new[]{"http://discord.com/api/webhooks/1/a","https://discord.com.attacker.invalid/api/webhooks/1/a","https://discord.com/api/webhooks/1/a?query=1","https://example.invalid/a"}){bool rejected=false;try{ValidateWebhook(invalid);}catch(ArgumentException){rejected=true;}Require(rejected,"noncanonical webhook rejected");}
            var json=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(DiscordPayload("Test","Local validation only"));var mentions=(Dictionary<string,object>)json["allowed_mentions"];Require(((System.Collections.IEnumerable)mentions["parse"]).Cast<object>().Count()==0,"Discord mentions disabled");
            byte[] input=Encoding.UTF8.GetBytes("local test value");byte[] encrypted=ProtectedData.Protect(input,null,DataProtectionScope.CurrentUser);byte[] plain=ProtectedData.Unprotect(encrypted,null,DataProtectionScope.CurrentUser);Require(input.SequenceEqual(plain),"DPAPI round trip");Array.Clear(input,0,input.Length);Array.Clear(plain,0,plain.Length);
            string temp=Path.Combine(Path.GetTempPath(),"CraftHarborChecks",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);string zip=Path.Combine(temp,"invalid.zip");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)){using(var w=new StreamWriter(archive.CreateEntry("../outside.txt").Open()))w.Write("invalid");}bool blocked=false;try{ExtractValidated(zip,Path.Combine(temp,"stage"));}catch(InvalidDataException){blocked=true;}Require(blocked&&!File.Exists(Path.Combine(temp,"outside.txt")),"ZIP traversal blocked");
            File.WriteAllText(report,"PASS: memory policy, webhook URL validation, mention suppression, DPAPI round trip, ZIP traversal rejection. No network calls or server startup.");return 0;
        }catch(Exception e){File.WriteAllText(report,"FAIL: "+e);return 1;}
    }
}
