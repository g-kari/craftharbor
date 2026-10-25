using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class DistanceOptions
{
    public bool Automatic=true;
    public int ExpectedPlayers=4,ManualView=8,ManualSimulation=6;
}
class DistancePlan { public int View,Simulation,CpuShare;public string Reason; }
partial class CraftHarbor
{
    DistanceOptions distances=new DistanceOptions();
    CheckBox autoDistances;
    NumericUpDown expectedPlayers;
    Label distancePreview;
    bool updatingDistances;
    DateTime distancePreviewAt=DateTime.MinValue;
    string DistanceFile {get{return Path.Combine(root,"performance-settings.json");}}
    bool HasModWorkload {get{return profile.ClientMode=="modpack"||new[]{"mods","plugins"}.Any(d=>Directory.Exists(Path.Combine(ServerDir,d))&&Directory.GetFiles(Path.Combine(ServerDir,d),"*.jar").Length>0);}}
    static DistancePlan PlanDistances(int logicalCpu,int heap,int registered,bool modded,int players)
    {
        int cpu=Math.Max(1,logicalCpu/Math.Max(1,registered));
        int tier=Math.Min(cpu<4?0:cpu<8?1:2,heap<6?0:heap<10?1:2);
        tier=Math.Max(0,tier-(players>=16?2:players>=8?1:0));
        return new DistancePlan{View=(modded?new[]{6,8,10}:new[]{6,8,12})[tier],Simulation=(modded?new[]{4,5,6}:new[]{4,6,8})[tier],CpuShare=cpu,
            Reason="論理CPUの目安 "+cpu+" / 割当上限 "+heap+" GiB / 登録 "+Math.Max(1,registered)+"台 / "+(modded?"MOD・プラグインあり":"Vanilla")+" / 想定 "+players+"人"};
    }
    static bool SupportsSimulation(string version,string properties)
    {
        var match=Regex.Match(version??"",@"^1\.(\d+)(?:\.|$)");
        if(match.Success)return int.Parse(match.Groups[1].Value)>=18;
        // Unknown versions are only changed when the server already exposes the setting.
        return Regex.IsMatch(properties,@"(?m)^simulation-distance=");
    }
    void InitializeDistanceSettings(FlowLayoutPanel form)
    {
        distances.ManualView=(int)viewSetting.Value;distances.ManualSimulation=(int)simulationSetting.Value;
        var current=ReadProperties();int value;
        if(int.TryParse(current["view-distance"],out value))distances.ManualView=ClampDistance(value);
        if(int.TryParse(current["simulation-distance"],out value))distances.ManualSimulation=ClampDistance(value);
        if(File.Exists(DistanceFile))try{distances=new JavaScriptSerializer().Deserialize<DistanceOptions>(File.ReadAllText(DistanceFile))??distances;}catch(Exception){distances.Automatic=false;Add("[距離設定] 保存済み設定を読めません。手動設定を使用します。");}
        distances.ManualView=ClampDistance(distances.ManualView);distances.ManualSimulation=ClampDistance(distances.ManualSimulation);distances.ExpectedPlayers=Math.Max(1,Math.Min(100,distances.ExpectedPlayers));
        var group=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0,12,0,12)};
        autoDistances=new HarborCheckBox{Text="PC性能に合わせて距離を自動設定する",Checked=distances.Automatic,AutoSize=true,AccessibleName="描画距離とシミュレーション距離の自動調整"};group.Controls.Add(autoDistances);
        expectedPlayers=NumberSetting(group,"同時接続人数の目安",1,100,distances.ExpectedPlayers);expectedPlayers.AccessibleName="自動調整で想定する同時接続人数";
        distancePreview=new Label{AutoSize=true,ForeColor=Muted,MaximumSize=new Size(740,0),AccessibleName="距離の適用予定と判断理由"};group.Controls.Add(distancePreview);
        FlowBtn(group,"次回の距離設定を保存",delegate{SaveDistanceSettings();RefreshDistancePreview();Add("[距離設定] 次回起動用の設定を保存しました。稼働中の設定は変更していません。");});
        form.Controls.Add(group);form.Controls.SetChildIndex(group,4);
        autoDistances.CheckedChanged+=delegate{distances.Automatic=autoDistances.Checked;RefreshDistancePreview();};
        expectedPlayers.ValueChanged+=delegate{distances.ExpectedPlayers=(int)expectedPlayers.Value;RefreshDistancePreview();};
        viewSetting.ValueChanged+=delegate{if(!updatingDistances&&!autoDistances.Checked)distances.ManualView=(int)viewSetting.Value;};
        simulationSetting.ValueChanged+=delegate{if(!updatingDistances&&!autoDistances.Checked)distances.ManualSimulation=(int)simulationSetting.Value;};
    }
    static int ClampDistance(int n){return Math.Max(2,Math.Min(32,n));}
    int DistancePreviewHeap(){return autoMemory==null||autoMemory.Checked?CurrentSharedBudget(false):Math.Min((int)memory.Value,CurrentSharedBudget(true));}
    void RefreshDistancePreview()
    {
        if(autoDistances==null)return;distancePreviewAt=DateTime.UtcNow;int heap=DistancePreviewHeap();
        var plan=PlanDistances(Environment.ProcessorCount,heap,RegisteredCount(),HasModWorkload,(int)expectedPlayers.Value);
        string props=File.ReadAllText(Path.Combine(ServerDir,"server.properties"));bool simulation=SupportsSimulation(profile.MinecraftVersion,props);
        updatingDistances=true;try{viewSetting.Value=autoDistances.Checked?plan.View:distances.ManualView;simulationSetting.Value=autoDistances.Checked?plan.Simulation:distances.ManualSimulation;}finally{updatingDistances=false;}
        viewSetting.Enabled=!autoDistances.Checked;simulationSetting.Enabled=!autoDistances.Checked&&simulation;expectedPlayers.Enabled=autoDistances.Checked;
        distancePreview.Text=(autoDistances.Checked?"次回の自動設定":"次回の手動設定")+"：描画 "+viewSetting.Value+" / "+(simulation?"シミュレーション "+simulationSetting.Value:"シミュレーションは非対応・未確認")+" chunks\n"+
            (autoDistances.Checked?plan.Reason+"\n":"")+"起動直前に再計算します。人数は見積もり用で、最大参加人数は変更しません。\n保守的な初期値です。TPS・MSPTを確認して手動調整できます。"+(heap<4?"\n空きメモリが不足しています。起動時に再確認します。":"");
    }
    void SaveDistanceSettings()
    {
        if(autoDistances==null)return;distances.Automatic=autoDistances.Checked;distances.ExpectedPlayers=(int)expectedPlayers.Value;
        string temp=DistanceFile+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(distances),new UTF8Encoding(false));
        if(File.Exists(DistanceFile))File.Replace(temp,DistanceFile,DistanceFile+".bak");else File.Move(temp,DistanceFile);
    }
    static string DistanceProperties(string original,int view,int simulation,bool supported)
    {
        string text=original;var values=new Dictionary<string,int>{{"view-distance",ClampDistance(view)}};if(supported)values.Add("simulation-distance",ClampDistance(simulation));
        foreach(var pair in values){string pattern=@"(?m)^"+pair.Key+@"=[^\r\n]*";string line=pair.Key+"="+pair.Value;if(Regex.IsMatch(text,pattern))text=Regex.Replace(text,pattern,line);else text=text.TrimEnd()+"\r\n"+line+"\r\n";}return text;
    }
    void ApplyDistancesAtStartup(int heap)
    {
        EnsureStopped(true);
        Add(ApplyStartupDistances(ServerDir,profile.MinecraftVersion,heap,RegisteredCount(),HasModWorkload,distances));
    }
    static string ApplyStartupDistances(string directory,string version,int heap,int registered,bool modded,DistanceOptions options)
    {
        var plan=PlanDistances(Environment.ProcessorCount,heap,registered,modded,options.ExpectedPlayers);
        int view=options.Automatic?plan.View:ClampDistance(options.ManualView);
        int simulation=options.Automatic?plan.Simulation:ClampDistance(options.ManualSimulation);
        string path=Path.Combine(directory,"server.properties"),original=File.ReadAllText(path);
        bool supported=SupportsSimulation(version,original);string next=DistanceProperties(original,view,simulation,supported);
        if(next!=original){string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temp,next,new UTF8Encoding(false));File.Replace(temp,path,path+".before-distance.bak");}
        return "[距離設定] "+(options.Automatic?"自動":"手動")+"：描画 "+view+(supported?" / シミュレーション "+simulation:"")+" chunks。"+(options.Automatic?plan.Reason:"");
    }
    static void CheckDistancePolicy()
    {
        var low=PlanDistances(2,4,1,true,4);Require(low.View==6&&low.Simulation==4,"low spec distance budget");
        var high=PlanDistances(24,33,1,true,4);Require(high.View==10&&high.Simulation==6,"modded high spec conservative cap");
        var vanilla=PlanDistances(24,57,1,false,4);Require(vanilla.View==12&&vanilla.Simulation==8,"vanilla distance budget");
        Require(PlanDistances(24,57,1,true,4).View==high.View,"extra RAM alone cannot unbound distances");
        Require(PlanDistances(8,8,4,true,4).View<PlanDistances(8,8,1,true,4).View,"multi-server CPU sharing");
        Require(PlanDistances(24,33,1,true,16).View<high.View,"expected player load reduces distance");
        string original="# keep\r\nonline-mode=true\r\nserver-port=25565\r\nview-distance=8\r\nsimulation-distance=6\r\ncustom=value\r\n";
        string tuned=DistanceProperties(original,10,6,true);Require(tuned==original.Replace("view-distance=8","view-distance=10"),"distance tuning preserves unrelated properties");Require(DistanceProperties(tuned,10,6,true)==tuned,"distance tuning idempotent");
        Require(!SupportsSimulation("1.17.1",original)&&SupportsSimulation("1.18.2",original)&&!SupportsSimulation("", "view-distance=8"),"simulation version compatibility");
        Require(!DistanceProperties("view-distance=8\n",6,4,false).Contains("simulation-distance"),"unsupported simulation property not introduced");
    }
    void CheckDistanceWorkflow()
    {
        string path=Path.Combine(ServerDir,"server.properties"),before=File.ReadAllText(path);
        autoDistances.Checked=false;viewSetting.Value=9;simulationSetting.Value=5;expectedPlayers.Value=12;SaveDistanceSettings();
        var saved=new JavaScriptSerializer().Deserialize<DistanceOptions>(File.ReadAllText(DistanceFile));Require(!saved.Automatic&&saved.ManualView==9&&saved.ManualSimulation==5&&saved.ExpectedPlayers==12,"per-profile manual tuning persisted");
        Require(File.ReadAllText(path)==before,"saving next-start settings does not change running properties");
        using(var listener=new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,System.Net.Sockets.SocketType.Stream,System.Net.Sockets.ProtocolType.Tcp)){
            listener.ExclusiveAddressUse=true;listener.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,ServerPort));listener.Listen(1);bool rejected=false;
            try{ApplyDistancesAtStartup(12);}catch(System.Net.Sockets.SocketException){rejected=true;}Require(rejected&&File.ReadAllText(path)==before,"busy server port blocks live tuning");
        }
        ApplyDistancesAtStartup(12);var values=ReadProperties();Require(values["view-distance"]=="9"&&values["simulation-distance"]=="5","manual distances applied before next launch");Require(File.ReadAllText(path+".before-distance.bak")==before,"original properties backed up before tuning");
        File.WriteAllText(path,before,new UTF8Encoding(false));expectedPlayers.Value=4;autoDistances.Checked=true;SaveDistanceSettings();
    }
}
