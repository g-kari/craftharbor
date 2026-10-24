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

partial class AstralManager : Form
{
    readonly string root = WorkspaceRoot;
    readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
    Process server, tunnel;
    volatile bool ready, stopping, backupBusy;
    bool closing, quitting;
    DateTime stopAt;
    string claimUrl = "";
    Label serverState, tunnelState, detail;
    TextBox log, address, command, player;
    NumericUpDown memory;
    Button start, stop, connect, disconnect, backup;
    System.Windows.Forms.Timer timer;
    static readonly Color Bg = Color.FromArgb(18,23,33), Card = Color.FromArgb(28,35,48), Ink = Color.FromArgb(232,237,245), Muted = Color.FromArgb(155,170,189), Accent = Color.FromArgb(111,218,192);
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
        if(args.Length>0&&args[0]=="--core-check")return CoreChecks();
        if(!InitializeWorkspace(args))return 1;
        RegisterWorkspace(WorkspaceRoot);
        bool first;
        using (var mutex = new Mutex(true,WorkspaceMutexName(WorkspaceRoot),out first))
        {
            if (!first) { MessageBox.Show("管理アプリはすでに起動しています。","Astral Manager"); return 1; }
            if (args.Length > 0 && args[0] == "--self-test") return SelfTest();
            if (args.Length > 0 && args[0] == "--metrics-replay") return ReplayMetrics();
            if (args.Length > 0 && args[0] == "--experience-check") return CheckExperience();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { MessageBox.Show(e.Exception.Message,"操作できませんでした"); };
            var manager = new AstralManager();
            if (Array.IndexOf(args,"--connect") >= 0) manager.Shown += delegate { try { manager.StartTunnel(); } catch(Exception e) { manager.Add("[playitエラー] "+e.Message); } };
            Application.Run(manager);
        }
        return 0;
    }
    public AstralManager()
    {
        LoadProfile();
        AutoScaleMode=AutoScaleMode.Dpi;
        Text = "Astral Server Manager — "+profile.Name;
        string iconFile=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"icons","astral.ico");if(File.Exists(iconFile))Icon=new Icon(iconFile);
        MinimumSize = new Size(1024,720);Size = new Size(Math.Min(1360,Screen.PrimaryScreen.WorkingArea.Width),Math.Min(960,Screen.PrimaryScreen.WorkingArea.Height));
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg; ForeColor = Ink; Font = new Font("Yu Gothic UI",10);
        var outer = new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(24), ColumnCount=1, RowCount=6 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute,70));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute,132));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute,92));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute,68));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        Controls.Add(outer);
        var heading = new FlowLayoutPanel { Dock=DockStyle.Fill, FlowDirection=FlowDirection.TopDown, WrapContents=false };
        heading.Controls.Add(new Label { Text="ASTRAL  /  SERVER CONTROL", Font=new Font("Segoe UI",21,FontStyle.Bold), ForeColor=Accent, AutoSize=true });
        heading.Controls.Add(new Label { Text=ProfileMetadata(), AutoSize=true, ForeColor=Muted });
        outer.Controls.Add(heading,0,0);
        var cards = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=2, RowCount=1 };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        var local = PanelCard(); var publicPanel = PanelCard(); cards.Controls.Add(local,0,0); cards.Controls.Add(publicPanel,1,0);
        serverState = LabelAt(local,"サーバー：停止中",16,12,16);
        tunnelState = LabelAt(publicPanel,"playit.gg：停止中",16,12,16);
        start=Btn(local,"サーバー起動",16,52,145,delegate { StartServer(); });
        stop=Btn(local,"保存して停止",170,52,145,delegate { StopServer(); });
        memory = new NumericUpDown { Location=new Point(330,57), Width=58, Minimum=4,Maximum=24,Value=6, BackColor=Bg,ForeColor=Ink };
        local.Controls.Add(memory); LabelAt(local,"GB",390,58,10);
        connect=Btn(publicPanel,"接続を開始",16,52,135,delegate { StartTunnel(); });
        disconnect=Btn(publicPanel,"接続を停止",160,52,135,delegate { StopTunnel(); });
        Btn(publicPanel,"設定ページ",304,52,135,delegate { Open("https://playit.gg/account/tunnels"); });
        outer.Controls.Add(cards,0,1);
        var join = new Panel { Dock=DockStyle.Fill, BackColor=Card, Margin=new Padding(0,6,0,6) };
        LabelAt(join,"友達に渡す接続先",16,8,10);
        var joinRow=new TableLayoutPanel { Dock=DockStyle.Bottom,Height=48,Padding=new Padding(16,0,16,8),ColumnCount=4,RowCount=1 };
        joinRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));joinRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,95));joinRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,85));joinRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,155));
        address = new TextBox { Dock=DockStyle.Fill, Font=new Font("Consolas",12),BackColor=Bg,ForeColor=Ink,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,6,10,0) };
        joinRow.Controls.Add(address,0,0);
        var copy=Btn(joinRow,"コピー",0,0,90,delegate { if(address.Text.Trim().Length>0) Clipboard.SetText(address.Text.Trim()); });copy.Dock=DockStyle.Fill;copy.Margin=new Padding(0,0,6,0);
        var save=Btn(joinRow,"保存",0,0,80,delegate { SaveSettings(); Add("[管理] 接続先を保存しました。"); });save.Dock=DockStyle.Fill;save.Margin=new Padding(0,0,6,0);
        var claim=Btn(joinRow,"アカウント連携",0,0,150,delegate { if(claimUrl.Length>0) Open(claimUrl); else { Open("https://playit.gg/account"); Add("[案内] 未連携なら『接続を開始』を押し、ログに表示される登録URLを開いてください。"); } });claim.Dock=DockStyle.Fill;claim.Margin=new Padding(0);
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
        logs.RowStyles.Add(new RowStyle(SizeType.Absolute,27)); logs.RowStyles.Add(new RowStyle(SizeType.Percent,100));logs.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
        detail = new Label { Text="自分は localhost:"+ServerPort+" に接続。友達には公開接続先を共有します。", ForeColor=Muted,Dock=DockStyle.Fill,AutoEllipsis=true }; logs.Controls.Add(detail,0,0);
        log=new TextBox { Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,BackColor=Color.FromArgb(11,16,24),ForeColor=Ink,Font=new Font("Consolas",10),BorderStyle=BorderStyle.FixedSingle }; logs.Controls.Add(log,0,1);
        var cmdPanel=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(0,6,0,0) };cmdPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));cmdPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90));
        command=new TextBox { Dock=DockStyle.Fill,BackColor=Card,ForeColor=Ink,Font=new Font("Consolas",11) };cmdPanel.Controls.Add(command,0,0);
        var send=new Button { Text="送信",Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=Card };send.Click+=delegate { SendCommand(); };cmdPanel.Controls.Add(send,1,0);
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
        timer=new System.Windows.Forms.Timer { Interval=400 };timer.Tick+=delegate { TickState(); };timer.Start();
        FormClosing+=OnClosing;
        Add("[案内] ①サーバー起動 → ②参加ユーザーを許可 → ③接続を開始 → ④playitでMinecraft Javaトンネルを作成。");
        Add("[playit設定] ローカル接続先は 127.0.0.1:"+ServerPort+"。発行された公開アドレスを上に保存してください。");
        Add("[案内] バックアップはサーバー停止中に作成します。管理アプリを閉じると、ワールドを保存して停止します。");
    }
    Panel PanelCard() { return new Panel { Dock=DockStyle.Fill,BackColor=Card,Margin=new Padding(0,0,8,8) }; }
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
            if(label=="MC") ParseServerMetrics(line);
            if(label=="playit") { var m=Regex.Match(line,@"https://playit\.gg/claim/[a-zA-Z0-9_-]+");if(m.Success)claimUrl=m.Value; }
            Add("["+label+"] "+line); };
        p.OutputDataReceived+=handler;p.ErrorDataReceived+=handler;
        p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();return p;
    }
    void StartServer()
    {
        if(Running(server)||backupBusy)return;
        using(var listener=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)) { listener.ExclusiveAddressUse=true;listener.Bind(new IPEndPoint(IPAddress.Loopback,ServerPort)); }
        string launchTarget=LaunchTarget();
        ready=false;stopping=false;SaveSettings();
        using(var memoryGate=new Mutex(false,"Local\\AstralMemoryReservations")){
            if(!memoryGate.WaitOne(5000))throw new Exception("他のサーバーのメモリ割当処理を待っています。もう一度お試しください。");
            try{int heap=ResolveHeapBudget();server=Launch(JavaPath(),"-Dfile.encoding=UTF-8 -Xms4G -Xmx"+heap+"G "+launchTarget,ServerDir,"MC");activeHeapGiB=heap;try{ReserveMemory(server,heap);}catch{server.StandardInput.WriteLine("stop");server.StandardInput.Flush();throw;}
                Add("[メモリ] "+(autoMemory.Checked?"自動・登録サーバー "+RegisteredCount()+"台":"手動")+"：最大 "+heap+" GiB / 初期 4 GiB。必要に応じてJVMが拡張します。");
            }finally{memoryGate.ReleaseMutex();}
        }
        ResetMetrics();
        Add("[管理] サーバーを起動しています。初回は数分かかる場合があります。");
    }
    void StopServer() { if(!Running(server)||stopping)return;Send("stop");stopping=true;stopAt=DateTime.UtcNow;Add("[管理] ワールドを保存して停止しています。"); }
    void StartTunnel()
    {
        if(Running(tunnel))return;
        tunnel=Launch(Path.Combine(PlayitDir,"playit-0.17.1.exe"),"--stdout --secret_path "+Q(Path.Combine(PlayitDir,"agent-secret.toml")),PlayitDir,"playit");
        Add("[管理] 初回はログの https://playit.gg/claim/... を『アカウント連携』ボタンで開きます。");
    }
    void StopTunnel() { if(Running(tunnel)) { tunnel.Kill();tunnel.WaitForExit(3000);Add("[管理] playit接続を停止しました。"); } }
    void Send(string text) { if(!Running(server)||stopping) { Add("[案内] サーバー起動後に操作してください。");return; }server.StandardInput.WriteLine(text);server.StandardInput.Flush();Add("> "+text); }
    void SendCommand() { string s=command.Text.Trim().TrimStart('/');if(s.Length==0)return;if(s=="stop")StopServer();else Send(s);command.Clear(); }
    void AllowPlayer(string op) { string n=player.Text.Trim();if(!Regex.IsMatch(n,@"^[A-Za-z0-9_]{1,16}$"))throw new Exception("Java版ユーザー名を入力してください（英数字と _ 、16文字以内）。");Send("whitelist "+op+" "+n); }
    void Backup()
    {
        if(Running(server)||backupBusy) { Add("[案内] サーバー停止後にバックアップしてください。");return; }
        // Port check also prevents a second, externally started server being backed up live.
        using(var s=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)) { s.ExclusiveAddressUse=true;s.Bind(new IPEndPoint(IPAddress.Loopback,ServerPort)); }
        backupBusy=true;string dest=Path.Combine(root,"backups","astral-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".zip");Directory.CreateDirectory(Path.GetDirectoryName(dest));Add("[管理] サーバー全体をバックアップしています…");
        Task.Run(delegate { try { ZipFile.CreateFromDirectory(ServerDir,dest+".partial",CompressionLevel.Fastest,false);File.Move(dest+".partial",dest);Add("[管理] バックアップ完了："+dest);NotifyDiscord("backup","完全バックアップ完了","サーバーの完全バックアップを保存しました。"); }catch(Exception e){Add("[バックアップ失敗] "+e.Message);NotifyDiscord("backup","バックアップ失敗","バックアップを作成できませんでした。主催者は管理アプリのログを確認してください。");}finally{backupBusy=false;} });
    }
    void TickState()
    {
        string s;var sb=new StringBuilder();int count=0;while(count++<400 && queue.TryDequeue(out s))sb.AppendLine(s);
        if(sb.Length>0) { if(log.TextLength>250000)log.Text=log.Text.Substring(log.TextLength-150000);log.AppendText(sb.ToString()); }
        bool sr=Running(server),tr=Running(tunnel);
        UpdateFeatures(sr,tr);
        serverState.Text="サーバー："+(sr?(stopping?"保存・停止中":ready?"稼働中":"起動中…"):"停止中");
        serverState.ForeColor=sr&&ready?Accent:Ink;
        tunnelState.Text="playit.gg："+(tr?(File.Exists(Path.Combine(PlayitDir,"agent-secret.toml"))?"実行中（ログを確認）":"アカウント連携待ち"):"停止中");
        start.Enabled=!sr&&!backupBusy&&!closing;stop.Enabled=sr&&!stopping;connect.Enabled=!tr&&!closing;disconnect.Enabled=tr;backup.Enabled=!sr&&!backupBusy&&!closing;memory.Enabled=!sr&&!autoMemory.Checked;
        if(stopping && sr && (DateTime.UtcNow-stopAt).TotalSeconds>120) detail.Text="停止に時間がかかっています。保存中の可能性があるため、そのまま待機しています。";
        if(closing&&!sr&&!backupBusy&&DiscordDrainedForExit()) { quitting=true;Close(); }
    }
    void OnClosing(object sender,FormClosingEventArgs e)
    {
        if(quitting)return;
        SaveSettings();
        if(backupBusy) { e.Cancel=true;MessageBox.Show("バックアップ完了後に閉じてください。");return; }
        if(Running(server))
        {
            e.Cancel=true;
            if(closing)return;
            if(MessageBox.Show("ワールドを保存してサーバーとplayit接続を停止します。","終了",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
            closing=true;StopTunnel();StopServer();return;
        }
        if(!DiscordDrainedForExit()){e.Cancel=true;closing=true;StopTunnel();Add("[Discord] 送信完了を待ってから終了します。");return;}
        StopTunnel();timer.Stop();
    }
    static int SelfTest()
    {
        string root=WorkspaceRoot, report=Path.Combine(root,"verification.txt");
        try
        {
            var p=new Process { StartInfo=new ProcessStartInfo(JavaPath(),"-version") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true } };
            p.Start();string v=p.StandardError.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0||!v.Contains("version"))throw new Exception("Java runtime unavailable: "+v);
            var manager=new AstralManager();manager.CreateControl();manager.RunFeatureChecks();if(manager.FtbBackupsAvailable){manager.backupHours.Value=2;manager.backupKeep.Value=7;manager.ApplyAutoBackup();}manager.StartServer();
            DateTime deadline=DateTime.UtcNow.AddMinutes(7);
            while(!manager.ready&&Running(manager.server)&&DateTime.UtcNow<deadline)Thread.Sleep(500);
            bool success=manager.ready;
            bool metricsOk=false;string metrics="";
            if(success){manager.UpdateFeatures(true,false);DateTime metricDeadline=DateTime.UtcNow.AddSeconds(35);while(manager.SparkAvailable&&DateTime.UtcNow<metricDeadline&&(double.IsNaN(manager.tps)||double.IsNaN(manager.mspt))){Thread.Sleep(500);manager.UpdateFeatures(true,false);}metricsOk=manager.ramGiB>0&&(!manager.SparkAvailable||(!double.IsNaN(manager.tps)&&!double.IsNaN(manager.mspt)));metrics="TPS="+manager.tps+", MSPT="+manager.mspt+", process RAM GiB="+manager.ramGiB+", CPU="+manager.cpuPercent+", players="+manager.onlineCount;}
            if(Running(manager.server)){manager.StopServer();if(!manager.server.WaitForExit(120000))throw new Exception("Graceful stop timed out");manager.server.WaitForExit();}
            var sb=new StringBuilder();string line;while(manager.queue.TryDequeue(out line))sb.AppendLine(line);File.WriteAllText(Path.Combine(root,"verification-server.log"),sb.ToString());
            if(!success)throw new Exception("Server did not become ready; see verification-server.log");
            if(manager.server.ExitCode!=0)throw new Exception("Server exit code: "+manager.server.ExitCode);
            if(!metricsOk)throw new Exception("Live metrics not acquired: "+metrics+"; see verification-server.log");
            File.WriteAllText(report,"PASS: Java runtime; all manager tabs created; actual server reached Done; graceful stop completed.\r\nPASS: live "+metrics+"\r\nPASS: backup extraction verified and traversal rejected; settings read; MOD and player lists loaded.\r\n"+DateTime.Now.ToString("s"));manager.timer.Stop();manager.Dispose();return 0;
        }
        catch(Exception e){File.WriteAllText(report,"FAIL: "+e);return 1;}
    }
}
