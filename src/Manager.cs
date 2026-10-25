using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

partial class CraftHarbor : Form
{
    readonly string root = WorkspaceRoot;
    readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
    Process server, tunnel;
    volatile bool ready, stopping, backupBusy;
    bool closing, quitting;
    bool preparingServer,cancelServerStart;
    DateTime stopAt;
    string claimUrl = "";
    Label serverState, tunnelState, detail;
    TextBox log, address, command, player;
    NumericUpDown memory;
    Button start, stop, connect, disconnect, backup;
    System.Windows.Forms.Timer timer;
    static readonly Color Bg = HarborTheme.Background, Card = HarborTheme.Panel, Ink = HarborTheme.Text, Muted = HarborTheme.Muted, Accent = HarborTheme.Live;
    string ServerDir { get { return Path.Combine(root,"server"); } }
    string PlayitDir { get { return Path.Combine(root,"playit"); } }
    string Settings { get { return Path.Combine(root,"manager-settings.txt"); } }
    static bool Running(Process p) { try { return p != null && !p.HasExited; } catch { return false; } }
    static string Q(string s) { return "\"" + s + "\""; }
    static string JavaPath()
    {
        string configured = Path.Combine(WorkspaceRoot,"java-path.txt");
        if (File.Exists(configured)) {string value=File.ReadAllText(configured).Trim();if(value.Length>0)return value;}
        return "java.exe";
    }
    [STAThread] static int Main(string[] args)
    {
#if HOST_BINARY
        if(Array.IndexOf(args,"--boot-check")>=0)return BootChecks(args);
        return PersistentHostMain(args);
#else
        if(Array.IndexOf(args,"--configure-boot-task")>=0)return ConfigureBootTask(args);
        if(Array.IndexOf(args,"--remove-boot-tasks")>=0)return RemoveInstalledBootTasks();
        if(Array.IndexOf(args,"--boot-check")>=0)return BootChecks(args);
        if(args.Length>0&&args[0]=="--host-check")return HostCheck(args);
        if(args.Length>0&&args[0]=="--pack-check")return PackCheck(args);
        if(args.Length>0&&args[0]=="--pack-download-check")return PackDownloadCheck(args);if(args.Length>0&&args[0]=="--metrics-readonly-check")return CheckLiveMetricRendering(args);
        if(args.Length>0&&args[0]=="--core-check")return CoreChecks();if(args.Length>0&&args[0]=="--java-check")return CheckInstalledJava(args);
        if(!InitializeWorkspace(args))return 1;
        if(Array.IndexOf(args,"--experience-check")>=0)ManagerDataOverride=Path.Combine(WorkspaceRoot,".manager-check-data");
        RegisterWorkspace(WorkspaceRoot);
        bool first;
        using (var mutex = new Mutex(true,WorkspaceMutexName(WorkspaceRoot),out first))
        {
            if (!first) { MessageBox.Show("管理アプリはすでに起動しています。","CraftHarbor"); return 1; }
            if (args.Length > 0 && args[0] == "--self-test") return SelfTest();
            if (args.Length > 0 && args[0] == "--metrics-replay") return ReplayMetrics();
            if (args.Length > 0 && args[0] == "--experience-check") return CheckExperience();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { MessageBox.Show(e.Exception.Message,"操作できませんでした"); };
            var manager = new CraftHarbor();
            if(Array.IndexOf(args,"--start-server")>=0)manager.Shown+=delegate{manager.StartServer();};
            if (Array.IndexOf(args,"--connect") >= 0) manager.Shown += delegate { try { manager.StartTunnel(); } catch(Exception e) { manager.Add("[playitエラー] "+e.Message); } };
            Application.Run(manager);
        }
        return 0;
#endif
    }
    public CraftHarbor()
    {
        LoadProfile();LoadHosting();
        AutoScaleMode=AutoScaleMode.Dpi;
        Text = "CraftHarbor — "+profile.Name;
        string iconFile=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"icons","craftharbor.ico");if(File.Exists(iconFile))Icon=new Icon(iconFile);
        MinimumSize = new Size(1024,720);Size = new Size(Math.Min(1360,Screen.PrimaryScreen.WorkingArea.Width),Math.Min(960,Screen.PrimaryScreen.WorkingArea.Height));
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg; ForeColor = Ink; Font = new Font("Yu Gothic UI",12);
        var outer = new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(24), ColumnCount=1, RowCount=6 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute,70));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute,132));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute,92));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute,68));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        Controls.Add(outer);
        var heading = new FlowLayoutPanel { Dock=DockStyle.Fill, FlowDirection=FlowDirection.TopDown, WrapContents=false };
        heading.Controls.Add(new Label { Text="CRAFTHARBOR  /  SERVER CONTROL", Font=new Font("Segoe UI",21,FontStyle.Bold), ForeColor=Accent, AutoSize=true });
        heading.Controls.Add(new Label { Text=ProfileMetadata(), AutoSize=true, ForeColor=Muted });
        outer.Controls.Add(heading,0,0);
        var cards = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=2, RowCount=1 };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        var local = PanelCard(); var publicPanel = PanelCard(); cards.Controls.Add(local,0,0); cards.Controls.Add(publicPanel,1,0);
        serverState = LabelAt(local,"サーバー：停止中",16,12,16);
        tunnelState = LabelAt(publicPanel,"playit.gg：停止中",16,12,16);
        start=Btn(local,"サーバー起動",16,52,145,delegate { StartServer(); });
        stop=Btn(local,"保存して停止",170,52,145,delegate { StopServer(); });
        memory = new HarborNumber { Location=new Point(330,57), Width=58, Minimum=4,Maximum=24,Value=6, BackColor=Bg,ForeColor=Ink };
        local.Controls.Add(memory); LabelAt(local,"GB",390,58,10);
        connect=Btn(publicPanel,"接続を開始",16,52,135,delegate { StartTunnel(); });
        disconnect=Btn(publicPanel,"接続を停止",160,52,135,delegate { StopTunnel(); });
        Btn(publicPanel,"設定ページ",304,52,135,delegate { HostingSettings(); });
        outer.Controls.Add(cards,0,1);
        var join = new SurfacePanel { Dock=DockStyle.Fill, BackColor=Card, Margin=new Padding(0,6,0,6) };
        LabelAt(join,"友達に渡す接続先",16,8,10);
        var joinRow=new TableLayoutPanel { Dock=DockStyle.Bottom,Height=48,Padding=new Padding(16,0,16,8),ColumnCount=4,RowCount=1 };
        joinRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));joinRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,95));joinRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,85));joinRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,155));
        address = new TextBox { Dock=DockStyle.Fill, Font=new Font("Consolas",12),BackColor=Bg,ForeColor=Ink,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,6,10,0) };
        joinRow.Controls.Add(address,0,0);
        var copy=Btn(joinRow,"コピー",0,0,90,delegate { if(address.Text.Trim().Length>0) Clipboard.SetText(address.Text.Trim()); });copy.Dock=DockStyle.Fill;copy.Margin=new Padding(0,0,6,0);
        var save=Btn(joinRow,"保存",0,0,80,delegate { SaveSettings(); Add("[管理] 接続先を保存しました。"); });save.Dock=DockStyle.Fill;save.Margin=new Padding(0,0,6,0);
        var claim=Btn(joinRow,"アカウント連携",0,0,150,delegate { OpenHostingAccount(); });claim.Dock=DockStyle.Fill;claim.Margin=new Padding(0);
        join.Controls.Add(joinRow);
        outer.Controls.Add(join,0,2);
        var tools = new FlowLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(0,9,0,0), WrapContents=false };
        FlowBtn(tools,"一括起動",delegate { StartServer();StartTunnel(); });
        FlowBtn(tools,"再起動",delegate { RequestRestart(); });
        backup=FlowBtn(tools,"バックアップ",delegate { Backup(); });
        FlowBtn(tools,"保存先を開く",delegate { Open(ServerDir); });
        FlowBtn(tools,"バックアップ一覧",delegate { Directory.CreateDirectory(Path.Combine(root,"backups")); Open(Path.Combine(root,"backups")); });
        FlowBtn(tools,"参加者向け案内",delegate { workspace.SelectedIndex=7; });
        outer.Controls.Add(tools,0,3);
        var logs = new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3 };
        logs.RowStyles.Add(new RowStyle(SizeType.Absolute,36)); logs.RowStyles.Add(new RowStyle(SizeType.Percent,100));logs.RowStyles.Add(new RowStyle(SizeType.Absolute,76));
        detail = new Label { Text="自分は localhost:"+ServerPort+" に接続。友達には公開接続先を共有します。", ForeColor=Muted,Dock=DockStyle.Fill,AutoEllipsis=true }; logs.Controls.Add(detail,0,0);
        log=new TextBox { Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,BackColor=HarborTheme.Input,ForeColor=Ink,Font=new Font("Consolas",10),BorderStyle=BorderStyle.FixedSingle }; logs.Controls.Add(log,0,1);
        var cmdPanel=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(0,6,0,0) };cmdPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));cmdPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));
        command=new TextBox { Dock=DockStyle.Fill,BackColor=Card,ForeColor=Ink,Font=new Font("Consolas",11) };cmdPanel.Controls.Add(command,0,0);
        var send=new ActionButton { Text="送信",Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=Card };send.Click+=delegate { SendCommand(); };cmdPanel.Controls.Add(send,1,0);
        command.KeyDown+=delegate(object s,KeyEventArgs e) { if(e.KeyCode==Keys.Enter) { SendCommand(); e.SuppressKeyPress=true; } }; logs.Controls.Add(cmdPanel,0,2);outer.Controls.Add(logs,0,4);
        var allow = new FlowLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(0,10,0,0),WrapContents=false };
        allow.Controls.Add(new Label { Text="参加許可リスト",AutoSize=true,Margin=new Padding(0,5,12,0),ForeColor=Muted });
        player=new TextBox { Width=170,BackColor=Card,ForeColor=Ink }; allow.Controls.Add(player);
        FlowBtn(allow,"参加を許可",delegate { AllowPlayer("add"); });
        FlowBtn(allow,"許可を解除",delegate { AllowPlayer("remove"); });
        FlowBtn(allow,"許可リスト表示",delegate { Send("whitelist list"); });
        outer.Controls.Add(allow,0,5);
        InitializeFeatures(outer,logs);
        UpgradeExperience(outer,cards,join,heading,tools,allow);
        LoadSettings();
        LoadFeatureSettings();
        AttachPersistentHost();
        timer=new System.Windows.Forms.Timer { Interval=400 };timer.Tick+=delegate { TickState(); };timer.Start();
        FormClosing+=OnClosing;
        Add("[案内] サーバーを起動し、参加ユーザーを許可してください。公開方法は「"+HostingName+"」です。");
        Add("[公開] サーバーのポートは "+ServerPort+"。接続先を取得または保存して友達へ共有してください。");
        Add("[案内] バックアップはサーバー停止中に作成します。管理画面を閉じてもサーバーは稼働を続けます。");
    }
    Panel PanelCard() { return new SurfacePanel { Dock=DockStyle.Fill,BackColor=Card,Margin=new Padding(0,0,10,8) }; }
    Label LabelAt(Control p,string text,int x,int y,int size) { var l=new Label { Text=text,Location=new Point(x,y),AutoSize=true,Font=new Font("Yu Gothic UI",size),ForeColor=Ink };p.Controls.Add(l);return l; }
    Button Btn(Control p,string text,int x,int y,int w,Action action) { var b=new ActionButton { Text=text,Location=new Point(x,y),Size=new Size(w,40),FlatStyle=FlatStyle.Flat,BackColor=Card,ForeColor=Ink };b.FlatAppearance.BorderColor=Color.FromArgb(65,82,103);b.Click+=delegate { try { action(); } catch(Exception e) { Add("[エラー] "+e.Message);MessageBox.Show(e.Message,"操作できませんでした"); } };p.Controls.Add(b);return b; }
    Button FlowBtn(Control p,string text,Action action) { var b=Btn(p,text,0,0,Math.Max(132,text.Length*16+58),action);b.Margin=new Padding(0,0,8,0);return b; }
    void Add(string s) { queue.Enqueue(s); }
    void Open(string target) { Process.Start(new ProcessStartInfo(target) { UseShellExecute=true }); }
    void SaveSettings() { File.WriteAllLines(Settings,new string[] { ((int)memory.Value).ToString(),address.Text.Trim() },new UTF8Encoding(false)); SaveFeatureSettings(); }
    void LoadSettings() { if(!File.Exists(Settings))return;try { var s=File.ReadAllLines(Settings);int n;if(s.Length>0 && int.TryParse(s[0],out n) && n>=4 && n<=memory.Maximum)memory.Value=n;if(s.Length>1)address.Text=s[1]; }catch(Exception e){Add("[設定] "+e.Message);} }
    Process Launch(string exe,string args,string cwd,string label)
    {
        var p=new Process { StartInfo=new ProcessStartInfo(exe,args) { WorkingDirectory=cwd,UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8 } };
        DataReceivedEventHandler handler=delegate(object s,DataReceivedEventArgs e) { if(e.Data==null)return;string line=Regex.Replace(e.Data,@"\x1B\[[0-?]*[ -/]*[@-~]","");if(label=="MC" && line.Contains("Done (") && line.Contains("For help"))ready=true;
            line=Regex.Replace(line,"§.","");
            if(label=="MC") ParseServerMetrics(line);if(label=="ngrok"){var publicUrl=Regex.Match(line,@"tcp://([A-Za-z0-9.-]+:\d+)");if(publicUrl.Success)BeginInvoke((Action)delegate{address.Text=publicUrl.Groups[1].Value;SaveSettings();RefreshGuide();});}
            if(label=="playit") { var m=Regex.Match(line,@"https://playit\.gg/claim/[a-zA-Z0-9_-]+");if(m.Success)claimUrl=m.Value; }
            Add("["+label+"] "+line); };
        p.OutputDataReceived+=handler;p.ErrorDataReceived+=handler;
        p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();return p;
    }
    async void StartServer(){await StartServerAsync();}
    async Task StartServerAsync()
    {
        if(Running(server)||backupBusy||preparingServer)return;
        try{ApplyVoiceAutomatically();
        using(var listener=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)) { listener.ExclusiveAddressUse=true;listener.Bind(new IPEndPoint(IPAddress.Loopback,ServerPort)); }
        string launchTarget=LaunchTarget();
        ready=false;stopping=false;SaveSettings();
        preparingServer=true;cancelServerStart=false;start.Enabled=false;detail.Text="Javaのバージョンと起動オプションを確認しています…";serverState.Text="サーバー：Javaを確認中…";
        string java=JavaPath();var options=new JavaOptions{Automatic=javaOptions.Automatic,Custom=javaOptions.Custom};int previewHeap=ResolveHeapBudget();
        var verified=await Task.Run(()=>PrepareJavaPlan(java,previewHeap,options));
        if(cancelServerStart||IsDisposed||Disposing)return;
        using(var memoryGate=new Mutex(false,MemoryGateName())){
            if(!memoryGate.WaitOne(0))throw new Exception("他のサーバーがメモリを割り当てています。少し待ってからもう一度お試しください。");
            try{int heap=ResolveHeapBudget();var plan=PlanJava(verified.Runtime,heap,Environment.ProcessorCount,RegisteredCount(),HasModWorkload,options.Automatic,options.Custom);Add("[Java設定] "+plan.Reason+" / "+(plan.Tuning.Length==0?"JVM標準の調整":plan.Tuning));ApplyDistancesAtStartup(heap);server=LaunchPersistent("server",java,plan.Arguments+" "+launchTarget,ServerDir,heap);activeHeapGiB=heap;try{ReserveMemory(server,heap);}catch{HostCall(new HostRequest{Action="command",Text="stop"});throw;}
                Add("[メモリ] "+(autoMemory.Checked?"自動・登録サーバー "+RegisteredCount()+"台":"手動")+"：最大 "+heap+" GiB / 初期 "+plan.InitialHeap+" GiB。必要に応じてJVMが拡張します。");
            }finally{memoryGate.ReleaseMutex();}
        }
        ResetMetrics();
        RefreshProfileLabels();
        Add("[管理] サーバーを起動しています。初回は数分かかる場合があります。");
        }catch(Exception e){if(!IsDisposed){detail.Text="起動できませんでした："+e.Message;Add("[起動エラー] "+e.Message);}}finally{preparingServer=false;}
    }
    void StopServer() { if(!Running(server)||stopping)return;Send("stop");stopping=true;stopAt=DateTime.UtcNow;Add("[管理] ワールドを保存して停止しています。"); }
    void StartTunnel(){StartHosting();}
    void StopTunnel() { if(Running(tunnel)) { HostCall(new HostRequest{Action="stop-tunnel"});Add("[管理] 公開接続を停止しました。"); } }
    void Send(string text) { if(!Running(server)||stopping) { Add("[案内] サーバー起動後に操作してください。");return; }HostCall(new HostRequest{Action="command",Text=text});Add("> "+text); }
    void SendCommand() { string s=command.Text.Trim().TrimStart('/');if(s.Length==0)return;if(s=="stop")StopServer();else Send(s);command.Clear(); }
    void AllowPlayer(string op) { string n=player.Text.Trim();if(!Regex.IsMatch(n,@"^[A-Za-z0-9_]{1,16}$"))throw new Exception("Java版ユーザー名を入力してください（英数字と _ 、16文字以内）。");Send("whitelist "+op+" "+n); }
    void Backup()
    {
        CheckBootPreparation();
        if(Running(server)||backupBusy||preparingServer) { Add("[案内] サーバー停止・起動準備完了後にバックアップしてください。");return; }
        // Port check also prevents a second, externally started server being backed up live.
        using(var s=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)) { s.ExclusiveAddressUse=true;s.Bind(new IPEndPoint(IPAddress.Loopback,ServerPort)); }
        string dest=Path.Combine(root,"backups","craftharbor-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".zip");Directory.CreateDirectory(Path.GetDirectoryName(dest));
        var maintenance=WorkspaceMaintenanceLock(root);
        try{CheckBootPreparation();}catch{maintenance.Dispose();throw;}
        backupBusy=true;Add("[管理] サーバー全体をバックアップしています…");
        Task.Run(delegate { try { ZipFile.CreateFromDirectory(ServerDir,dest+".partial",CompressionLevel.Fastest,false);File.Move(dest+".partial",dest);Add("[管理] バックアップ完了："+dest);NotifyDiscord("backup","完全バックアップ完了","サーバーの完全バックアップを保存しました。"); }catch(Exception e){Add("[バックアップ失敗] "+e.Message);NotifyDiscord("backup","バックアップ失敗","バックアップを作成できませんでした。主催者は管理アプリのログを確認してください。");}finally{maintenance.Dispose();backupBusy=false;} });
    }
    void TickState()
    {
        PollPersistentHost();string s;var incoming=new System.Collections.Generic.List<string>();int count=0;while(count++<400&&queue.TryDequeue(out s))incoming.Add(s);AppendLogRecords(incoming);
        bool sr=Running(server),tr=Running(tunnel);
        UpdateFeatures(sr,tr);
        RefreshHosting();ApplyDiscoveredAddresses();RefreshDashboardPresentation(sr,tr);
        if(stopping && sr && (DateTime.UtcNow-stopAt).TotalSeconds>120) detail.Text="停止に時間がかかっています。保存中の可能性があるため、そのまま待機しています。";
        if(closing&&!backupBusy&&DiscordDrainedForExit()) { quitting=true;Close(); }
    }
    void OnClosing(object sender,FormClosingEventArgs e)
    {
        cancelServerStart=true;if(quitting)return;try{SaveSettings();}catch(Exception error){e.Cancel=true;MessageBox.Show(error.Message,"設定を確認してください");return;}
        if(backupBusy){e.Cancel=true;MessageBox.Show("バックアップ完了後に閉じてください。");return;}
        if(!DiscordDrainedForExit()){e.Cancel=true;closing=true;Add("[Discord] 送信完了を待ってから画面を閉じます。サーバーは稼働を続けます。");return;}
        timer.Stop();
        if(persistentAttached&&!Running(server)&&!Running(tunnel))try{HostCall(new HostRequest{Action="shutdown-idle"});}catch(Exception){}
    }
    static int SelfTest()
    {
        string root=WorkspaceRoot, report=Path.Combine(root,"verification.txt");
        try
        {
            var p=new Process { StartInfo=new ProcessStartInfo(JavaPath(),"-version") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true } };
            p.Start();string v=p.StandardError.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0||!v.Contains("version"))throw new Exception("Java runtime unavailable: "+v);
            var manager=new CraftHarbor();manager.CreateControl();manager.RunFeatureChecks();if(manager.FtbBackupsAvailable){manager.backupHours.Value=2;manager.backupKeep.Value=7;manager.ApplyAutoBackup();}var startup=manager.StartServerAsync();DateTime startupDeadline=DateTime.UtcNow.AddSeconds(30);while(!startup.IsCompleted&&DateTime.UtcNow<startupDeadline){Application.DoEvents();Thread.Sleep(30);}if(!startup.IsCompleted)throw new Exception("Startup preflight timed out");startup.GetAwaiter().GetResult();
            DateTime deadline=DateTime.UtcNow.AddMinutes(7);
            while(!manager.ready&&Running(manager.server)&&DateTime.UtcNow<deadline){manager.PollPersistentHost(true);Thread.Sleep(500);}
            bool success=manager.ready;
            bool metricsOk=false;string metrics="";
            if(success){manager.UpdateFeatures(true,false);DateTime metricDeadline=DateTime.UtcNow.AddSeconds(35);while(manager.SparkAvailable&&DateTime.UtcNow<metricDeadline&&(double.IsNaN(manager.tps)||double.IsNaN(manager.mspt))){Thread.Sleep(500);manager.PollPersistentHost(true);manager.UpdateFeatures(true,false);}metricsOk=manager.ramGiB>0&&(!manager.SparkAvailable||(!double.IsNaN(manager.tps)&&!double.IsNaN(manager.mspt)));metrics="TPS="+manager.tps+", MSPT="+manager.mspt+", process RAM GiB="+manager.ramGiB+", CPU="+manager.cpuPercent+", players="+manager.onlineCount;}
            if(Running(manager.server)){manager.StopServer();if(!manager.server.WaitForExit(120000))throw new Exception("Graceful stop timed out");manager.server.WaitForExit();manager.PollPersistentHost(true);}
            var sb=new StringBuilder();string line;while(manager.queue.TryDequeue(out line))sb.AppendLine(line);File.WriteAllText(Path.Combine(root,"verification-server.log"),sb.ToString());
            if(!success)throw new Exception("Server did not become ready; see verification-server.log");
            if(manager.serverExitCode!=0)throw new Exception("Server exit code: "+manager.serverExitCode);
            if(!metricsOk)throw new Exception("Live metrics not acquired: "+metrics+"; see verification-server.log");
            File.WriteAllText(report,"PASS: Java runtime; all manager tabs created; actual server reached Done; graceful stop completed.\r\nPASS: live "+metrics+"\r\nPASS: backup extraction verified and traversal rejected; settings read; MOD and player lists loaded.\r\n"+DateTime.Now.ToString("s"));manager.timer.Stop();manager.Dispose();return 0;
        }
        catch(Exception e){File.WriteAllText(report,"FAIL: "+e);return 1;}
    }
}
