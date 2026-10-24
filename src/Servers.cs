using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class RegisteredServer { public string Path=""; }
class MemoryReservation { public string Workspace="";public int Pid;public long StartTicks;public int HeapGiB; }

partial class CraftHarbor
{
    static string ManagerDataOverride;
    static string ManagerData {get{return ManagerDataOverride??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CraftHarbor");}}
    static string RegistryFile {get{return Path.Combine(ManagerData,"servers.json");}}
    static string ReservationsFile {get{return Path.Combine(ManagerData,"memory-reservations.json");}}
    static string CanonicalWorkspace(string path){string full=Path.GetFullPath(path);return full==Path.GetPathRoot(full)?full:full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);}
    static string WorkspaceMutexName(string path){using(var sha=SHA256.Create()){return "Local\\CraftHarborWorkspace-"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(CanonicalWorkspace(path).ToUpperInvariant()))).Replace("-","");}}
    static List<RegisteredServer> RegisteredServers()
    {
        if(!File.Exists(RegistryFile))return new List<RegisteredServer>();return new JavaScriptSerializer().Deserialize<List<RegisteredServer>>(File.ReadAllText(RegistryFile))??new List<RegisteredServer>();
    }
    static void SaveRegistry(List<RegisteredServer> servers)
    {
        AtomicManagerWrite(RegistryFile,new JavaScriptSerializer().Serialize(servers));
    }
    static void AtomicManagerWrite(string path,string text){Directory.CreateDirectory(ManagerData);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temp,text,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
    static void RegisterWorkspace(string path)
    {
        path=CanonicalWorkspace(path);using(var gate=new Mutex(false,"Local\\CraftHarborServerRegistry")){if(!gate.WaitOne(5000))throw new Exception("サーバー一覧の更新を待っています。もう一度お試しください。");try{var list=RegisteredServers();if(!list.Any(x=>string.Equals(CanonicalWorkspace(x.Path),path,StringComparison.OrdinalIgnoreCase))){list.Add(new RegisteredServer{Path=path});SaveRegistry(list);}}finally{gate.ReleaseMutex();}}
    }
    int RegisteredCount(){return Math.Max(1,RegisteredServers().Count(x=>ValidWorkspace(x.Path)));}
    static List<MemoryReservation> LiveReservations()
    {
        var list=File.Exists(ReservationsFile)?new JavaScriptSerializer().Deserialize<List<MemoryReservation>>(File.ReadAllText(ReservationsFile)):new List<MemoryReservation>();var live=new List<MemoryReservation>();foreach(var r in list??new List<MemoryReservation>())try{using(var p=Process.GetProcessById(r.Pid)){if(!p.HasExited&&p.StartTime.ToUniversalTime().Ticks==r.StartTicks)live.Add(r);}}catch(ArgumentException){}catch(InvalidOperationException){}return live;
    }
    static int SharedBudget(double total,double available,int count,double otherLimits,double otherUncommitted)
    {
        double capacity=total-Math.Max(4,total*.08);double share=Math.Floor(capacity/Math.Max(1,count));double remaining=Math.Min(capacity-otherLimits,available-Math.Max(2,total*.04)-otherUncommitted);return Math.Max(0,(int)Math.Floor(Math.Min(share,remaining)));
    }
    int CurrentSharedBudget(bool manual)
    {
        double limits=0,uncommitted=0;foreach(var r in LiveReservations()){if(string.Equals(CanonicalWorkspace(r.Workspace),CanonicalWorkspace(root),StringComparison.OrdinalIgnoreCase))continue;limits+=r.HeapGiB;try{using(var p=Process.GetProcessById(r.Pid))uncommitted+=Math.Max(0,r.HeapGiB-p.WorkingSet64/1073741824.0);}catch(ArgumentException){}}
        var m=PhysicalMemory();return SharedBudget(m[0],m[1],manual?1:RegisteredCount(),limits,uncommitted);
    }
    void ReserveMemory(Process process,int heap)
    {
        Directory.CreateDirectory(ManagerData);var live=LiveReservations();live.RemoveAll(r=>string.Equals(CanonicalWorkspace(r.Workspace),CanonicalWorkspace(root),StringComparison.OrdinalIgnoreCase));live.Add(new MemoryReservation{Workspace=CanonicalWorkspace(root),Pid=process.Id,StartTicks=process.StartTime.ToUniversalTime().Ticks,HeapGiB=heap});AtomicManagerWrite(ReservationsFile,new JavaScriptSerializer().Serialize(live));
    }
    void OpenServersDialog(bool snapshot=false)
    {
        using(var dialog=new Form{Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath),Text="登録サーバー",Size=new Size(1060,580),MinimumSize=new Size(940,500),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=Font}){
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(20)};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,64));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,64));dialog.Controls.Add(layout);
            layout.Controls.Add(new Label{Text="保存先ごとに別のサーバーを登録できます。別ウィンドウで開けば同時起動できます。\n自動メモリは登録数に応じて配分します。登録を増やした後は、既存サーバーも再起動すると再配分されます。",Dock=DockStyle.Fill,ForeColor=Muted},0,0);
            var grid=Grid();grid.AccessibleName="登録したサーバー一覧";grid.Columns.Add("name","サーバー名");grid.Columns.Add("path","保存先");grid.Columns.Add("port","ポート");grid.Columns.Add("state","状態");layout.Controls.Add(grid,0,1);
            Action refresh=delegate{grid.Rows.Clear();var live=LiveReservations();foreach(var item in RegisteredServers()){string name=Path.GetFileName(item.Path),port="—";bool valid=ValidWorkspace(item.Path);if(valid){string pf=Path.Combine(item.Path,"manager-profile.json");if(File.Exists(pf))try{name=new JavaScriptSerializer().Deserialize<ServerProfile>(File.ReadAllText(pf)).Name;}catch{}foreach(string line in File.ReadAllLines(Path.Combine(item.Path,"server","server.properties")))if(line.StartsWith("server-port="))port=line.Substring(12).Trim();}bool running=live.Any(x=>string.Equals(CanonicalWorkspace(x.Workspace),CanonicalWorkspace(item.Path),StringComparison.OrdinalIgnoreCase));grid.Rows.Add(name,item.Path,port,!valid?"保存先を確認":running?"起動中":"停止中");}};refresh();
            Func<string> selected=delegate{if(grid.SelectedRows.Count==0)throw new Exception("サーバーを選択してください。");return Convert.ToString(grid.SelectedRows[0].Cells[1].Value);};
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true,Padding=new Padding(0,10,0,0)};
            FlowBtn(actions,"URLから追加",delegate{ImportServerPack();refresh();});
            FlowBtn(actions,"既存サーバーを登録",delegate{using(var folder=new FolderBrowserDialog{Description="serverフォルダーの親フォルダーを選択",ShowNewFolderButton=false}){if(folder.ShowDialog()!=DialogResult.OK)return;if(!ValidWorkspace(folder.SelectedPath))throw new Exception("導入済みのJavaサーバーが見つかりません。");RegisterWorkspace(folder.SelectedPath);refresh();memoryPreviewAt=DateTime.MinValue;}});
            FlowBtn(actions,"この画面で切替",delegate{EnsureStopped();string path=selected();OpenRegistered(path,true);dialog.Close();});FlowBtn(actions,"別ウィンドウで開く",delegate{OpenRegistered(selected(),false);});FlowBtn(actions,"登録解除",delegate{string path=selected();if(string.Equals(CanonicalWorkspace(path),CanonicalWorkspace(root),StringComparison.OrdinalIgnoreCase))throw new Exception("現在のサーバーはこの画面から解除できません。");if(MessageBox.Show("一覧から登録を解除します。サーバーのファイルやワールドは削除しません。","登録解除",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;using(var gate=new Mutex(false,"Local\\CraftHarborServerRegistry")){if(!gate.WaitOne(5000))throw new Exception("一覧更新中です。");try{SaveRegistry(RegisteredServers().Where(x=>!string.Equals(CanonicalWorkspace(x.Path),CanonicalWorkspace(path),StringComparison.OrdinalIgnoreCase)).ToList());}finally{gate.ReleaseMutex();}}refresh();memoryPreviewAt=DateTime.MinValue;});layout.Controls.Add(actions,0,2);
            if(snapshot)dialog.Shown+=delegate{dialog.BeginInvoke((Action)delegate{Application.DoEvents();using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(root,"ui-servers.png"),System.Drawing.Imaging.ImageFormat.Png);}dialog.Close();});};
            dialog.ShowDialog(this);
        }
    }
    void OpenRegistered(string path,bool replace)
    {
        if(!ValidWorkspace(path))throw new Exception("保存先にサーバーが見つかりません。");if(string.Equals(CanonicalWorkspace(path),CanonicalWorkspace(root),StringComparison.OrdinalIgnoreCase))return;RegisterWorkspace(path);if(replace){SaveSettings();StopTunnel();File.WriteAllText(Path.Combine(ManagerData,"workspace.txt"),path);}
        Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--workspace "+Q(path)){UseShellExecute=false});if(replace){quitting=true;Close();}
    }
}
