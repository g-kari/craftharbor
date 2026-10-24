using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class JavaRuntime { public int Major;public bool Hotspot; }
class JavaOptions { public bool Automatic=true;public string Custom=""; }
class JavaPlan { public int InitialHeap;public string Tuning="",Arguments="",Reason="";public JavaRuntime Runtime; }
partial class CraftHarbor
{
    JavaOptions javaOptions=new JavaOptions();
    CheckBox autoJava;
    TextBox customJava,javaPreview;
    JavaRuntime previewRuntime;
    string previewJavaPath="";
    string javaProbeError="";
    bool javaProbeBusy;
    string JavaOptionsFile {get{return Path.Combine(root,"java-options.json");}}
    static JavaRuntime ParseJavaRuntime(string output)
    {
        var version=Regex.Match(output,@"(?m)^\s*java\.specification\.version\s*=\s*(\d+)(?:\.(\d+))?");
        var bits=Regex.Match(output,@"(?m)^\s*sun\.arch\.data\.model\s*=\s*(\d+)");
        if(!version.Success||!bits.Success||bits.Groups[1].Value!="64")throw new InvalidOperationException("64bit版Javaのバージョンを確認できません。サーバー設定で使用するjava.exeを指定してください。");
        int major=int.Parse(version.Groups[1].Value);if(major==1&&version.Groups[2].Success)major=int.Parse(version.Groups[2].Value);
        if(major<8)throw new InvalidOperationException("Java 8以上の64bitランタイムを指定してください。");
        var vm=Regex.Match(output,@"(?m)^\s*java\.vm\.name\s*=\s*(.+)$");string name=vm.Groups[1].Value;
        return new JavaRuntime{Major=major,Hotspot=name.IndexOf("OpenJ9",StringComparison.OrdinalIgnoreCase)<0&&(name.Contains("HotSpot")||name.Contains("Server VM"))};
    }
    static string RunJavaProbe(string exe,string arguments)
    {
        using(var process=new Process{StartInfo=new ProcessStartInfo(exe,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true}}){
            process.Start();var stderr=process.StandardError.ReadToEndAsync();var stdout=process.StandardOutput.ReadToEndAsync();
            if(!process.WaitForExit(8000)){try{process.Kill();}catch(InvalidOperationException){}throw new InvalidOperationException("Javaの確認がタイムアウトしました。java.exeの指定を確認してください。");}
            string output=stderr.GetAwaiter().GetResult()+stdout.GetAwaiter().GetResult();
            if(process.ExitCode!=0)throw new InvalidOperationException("Javaが起動オプションを受け付けませんでした。Javaの指定または手動オプションを確認してください。");return output;
        }
    }
    static string ValidateJavaCustom(string text)
    {
        if(text==null)text="";if(text.Length>3000)throw new ArgumentException("Javaの手動オプションは3000文字以内で指定してください。");
        var tokens=Regex.Split(text.Trim(),@"\s+").Where(t=>t.Length>0).ToArray();
        foreach(string token in tokens){if(!Regex.IsMatch(token,@"^-(?:XX:[+-]?[A-Za-z][A-Za-z0-9]*(?:=[A-Za-z0-9._:+/-]+)?|Xss[0-9]+[kKmM])$")||Regex.IsMatch(token,@"^-XX:(?:MaxHeapSize|InitialHeapSize|MinHeapSize)="))throw new ArgumentException("手動設定には -XX:... または -Xss... を空白で区切って指定してください。ヒープサイズはメモリ設定から変更します。");}
        return string.Join(" ",tokens);
    }
    static JavaPlan PlanJava(JavaRuntime runtime,int heap,int logicalCpu,int registered,bool modded,bool automatic,string custom)
    {
        if(heap<1)throw new ArgumentOutOfRangeException("heap");int initial=Math.Min(heap,modded?4:2);int cpu=Math.Max(1,logicalCpu/Math.Max(1,registered));string tuning="";
        if(automatic&&runtime!=null&&runtime.Hotspot){tuning="-XX:+UseG1GC -XX:MaxGCPauseMillis=200 -XX:+ParallelRefProcEnabled";
            if(cpu<logicalCpu){if(runtime.Major>=10)tuning+=" -XX:ActiveProcessorCount="+cpu;else tuning+=" -XX:ParallelGCThreads="+Math.Min(8,cpu)+" -XX:ConcGCThreads="+Math.Max(1,Math.Min(8,cpu)/4);}}
        if(!automatic)tuning=ValidateJavaCustom(custom);
        return new JavaPlan{InitialHeap=initial,Tuning=tuning,Arguments="-Dfile.encoding=UTF-8 "+tuning+" -Xms"+initial+"G -Xmx"+heap+"G",Reason="初期 "+initial+" GiB / 最大 "+heap+" GiB / 論理CPUの目安 "+cpu+" / "+(runtime==null?"Javaは起動前に確認":("Java "+runtime.Major+(runtime.Hotspot?"・HotSpot系":"・VM標準GC")))};
    }
    void InitializeJavaSettings(FlowLayoutPanel form)
    {
        if(File.Exists(JavaOptionsFile))try{javaOptions=new JavaScriptSerializer().Deserialize<JavaOptions>(File.ReadAllText(JavaOptionsFile))??javaOptions;}catch(Exception){Add("[Java設定] 設定を読めないため自動設定を使用します。");}
        var group=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0,0,0,18)};
        group.Controls.Add(new Label{Text="Javaの起動オプション",AutoSize=true,Font=new Font("Yu Gothic UI",14,FontStyle.Bold)});
        autoJava=new HarborCheckBox{Text="PC性能とJavaに合わせて自動設定する",AutoSize=true,Checked=javaOptions.Automatic};group.Controls.Add(autoJava);
        group.Controls.Add(new Label{Text="手動オプション（自動をオフにすると編集できます）",AutoSize=true});
        customJava=new TextBox{Width=700,Height=74,Multiline=true,ScrollBars=ScrollBars.Vertical,BackColor=Card,ForeColor=Ink,Text=javaOptions.Custom??"",Enabled=!autoJava.Checked,AccessibleName="Javaの手動起動オプション"};group.Controls.Add(customJava);
        javaPreview=new TextBox{Width=700,Height=112,Multiline=true,ReadOnly=true,BackColor=Card,ForeColor=Ink,ScrollBars=ScrollBars.Vertical,AccessibleName="次回起動時のJavaオプション"};group.Controls.Add(javaPreview);
        var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=true,MaximumSize=new Size(740,0)};group.Controls.Add(actions);
        var probe=FlowBtn(actions,"Javaを確認してプレビュー",async delegate{var button=actions.Controls.OfType<Button>().First();button.Enabled=false;javaProbeBusy=true;javaProbeError="";string exe=JavaPath();previewJavaPath=exe;previewRuntime=null;javaPreview.Text="JavaのバージョンとVMを確認しています…";try{var runtime=await Task.Run(()=>ParseJavaRuntime(RunJavaProbe(exe,"-XshowSettings:properties -version")));if(IsDisposed)return;previewRuntime=runtime;previewJavaPath=exe;RefreshJavaPreview();}catch(Exception e){if(!IsDisposed){javaProbeError=e.Message;javaPreview.Text=e.Message;}}finally{javaProbeBusy=false;if(!button.IsDisposed){button.Enabled=true;RefreshJavaPreview();}}});
        FlowBtn(actions,"Java設定を保存",delegate{SaveJavaSettings();RefreshJavaPreview();Add("[Java設定] 次回起動時に適用します。稼働中のJVMは変更していません。");});
        group.Controls.Add(new Label{Text="ヒープ上限は空きメモリと他サーバーの予約分から決定します。自動GCはHotSpot系JavaでG1を使用し、細部はJVMの自動調整に任せます。\n停止時間200msは目標値です。起動前にJavaの対応オプションを確認します。設定は次回起動時に反映します。",ForeColor=Muted,AutoSize=true,MaximumSize=new Size(740,0)});
        autoJava.CheckedChanged+=delegate{customJava.Enabled=!autoJava.Checked;RefreshJavaPreview();};customJava.TextChanged+=delegate{RefreshJavaPreview();};
        form.Controls.Add(group);form.Controls.SetChildIndex(group,1);RefreshJavaPreview();
    }
    void RefreshJavaPreview()
    {
        if(javaPreview==null||javaProbeBusy)return;if(previewJavaPath!=JavaPath()){javaProbeError="";previewRuntime=null;}if(javaProbeError.Length>0){javaPreview.Text="Javaの確認に失敗しました："+javaProbeError+"\r\n設定を確認して再試行してください。";return;}try{var runtime=previewJavaPath==JavaPath()?previewRuntime:null;var plan=PlanJava(runtime,Math.Max(1,DistancePreviewHeap()),Environment.ProcessorCount,RegisteredCount(),HasModWorkload,autoJava.Checked,customJava.Text);javaPreview.Text=plan.Reason+"\r\n"+plan.Arguments+(runtime==null&&autoJava.Checked?"\r\nGC・CPUの追加設定はJavaを確認すると表示します。":"");}catch(ArgumentException e){javaPreview.Text=e.Message;}
    }
    void SaveJavaSettings()
    {
        if(autoJava==null)return;string options=autoJava.Checked?customJava.Text:ValidateJavaCustom(customJava.Text);javaOptions=new JavaOptions{Automatic=autoJava.Checked,Custom=options};string temp=JavaOptionsFile+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(javaOptions),new UTF8Encoding(false));if(File.Exists(JavaOptionsFile))File.Replace(temp,JavaOptionsFile,JavaOptionsFile+".bak");else File.Move(temp,JavaOptionsFile);
    }
    JavaPlan PrepareJavaPlan(string exe,int heap,JavaOptions options)
    {
        var runtime=ParseJavaRuntime(RunJavaProbe(exe,"-XshowSettings:properties -version"));
        var plan=PlanJava(runtime,heap,Environment.ProcessorCount,RegisteredCount(),HasModWorkload,options.Automatic,options.Custom);plan.Runtime=runtime;
        // A small, separate -version process checks flag compatibility without starting Minecraft or committing the configured server heap.
        RunJavaProbe(exe,plan.Tuning+" -Xms32m -Xmx64m -version");return plan;
    }
    static void CheckJavaPolicy()
    {
        var modern=ParseJavaRuntime("java.specification.version = 17\nsun.arch.data.model = 64\njava.vm.name = OpenJDK 64-Bit Server VM\n");var plan=PlanJava(modern,33,24,2,true,true,"");Require(plan.InitialHeap==4&&plan.Arguments.Contains("-Xmx33G")&&plan.Tuning.Contains("ActiveProcessorCount=12"),"modern Java CPU share and heap plan");
        var legacy=ParseJavaRuntime("java.specification.version = 1.8\nsun.arch.data.model = 64\njava.vm.name = Java HotSpot(TM) 64-Bit Server VM\n");Require(!PlanJava(legacy,4,8,4,false,true,"").Tuning.Contains("ActiveProcessorCount"),"legacy Java uses compatible GC thread options");
        Require(PlanJava(new JavaRuntime{Major=21,Hotspot=false},8,8,1,false,true,"").Tuning=="","other VMs retain their supported GC defaults");
        var manual=PlanJava(modern,8,8,1,false,false,"-XX:+UseZGC -Xss1M");Require(!manual.Tuning.Contains("UseG1GC")&&manual.Tuning.Contains("UseZGC"),"manual collector is not combined with automatic G1");
        foreach(string invalid in new[]{"-Xmx64G","-XX:MaxHeapSize=64G","-jar other.jar","@args.txt","-XX:Foo=1\""}){bool rejected=false;try{ValidateJavaCustom(invalid);}catch(ArgumentException){rejected=true;}Require(rejected,"manual options cannot replace the heap budget or launch target");}
    }
    static int CheckInstalledJava(string[] args)
    {
        if(args.Length!=2)return 2;string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"java-verification.txt");try{
            var runtime=ParseJavaRuntime(RunJavaProbe(args[1],"-XshowSettings:properties -version"));var plan=PlanJava(runtime,16,Environment.ProcessorCount,2,true,true,"");
            RunJavaProbe(args[1],plan.Tuning+" -Xms32m -Xmx64m -version");File.WriteAllText(report,"PASS: 64bit Java "+runtime.Major+" parsed; selected automatic GC/CPU flags accepted by an isolated low-memory -version process. No Minecraft server was started. Flags: "+plan.Tuning);return 0;
        }catch(Exception e){File.WriteAllText(report,"FAIL: "+e.Message);return 1;}
    }
}
