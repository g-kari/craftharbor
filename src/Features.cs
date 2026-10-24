using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

partial class AstralManager
{
    Label cpuValue,ramValue,tpsValue,msptValue,uptimeValue,onlineValue,diskValue,metricsNote;
    Chart history;
    DataGridView usersGrid,modsGrid,backupsGrid;
    TextBox broadcastText,searchText,motdSetting;
    ComboBox difficultySetting,modeSetting;
    NumericUpDown maxPlayersSetting,viewSetting,simulationSetting,backupHours,backupKeep;
    CheckBox pvpSetting,flightSetting,autoCrashRestart;
    CheckBox scheduledRestart;
    DateTimePicker restartTime;
    DateTime lastSample=DateTime.MinValue,lastPoll=DateTime.MinValue,lastTps=DateTime.MinValue,lastMspt=DateTime.MinValue,lastScheduled=DateTime.MinValue;
    TimeSpan lastCpu;
    double cpuPercent,ramGiB,tps=double.NaN,mspt=double.NaN;
    volatile bool expectTps,expectMspt;
    readonly object metricLock=new object();
    volatile string onlineNames="";
    volatile int onlineCount;
    bool previousRunning,restartPending;
    int crashAttempts;
    readonly List<string> samples=new List<string>();
    readonly string[] configKeys={"motd","difficulty","gamemode","max-players","view-distance","simulation-distance","pvp","allow-flight"};
    string FeatureSettings { get { return Path.Combine(root,"manager-options.json"); } }
    TabPage Page(TabControl tabs,string title)
    {
        var p=new TabPage(title) { BackColor=Bg,ForeColor=Ink,Padding=new Padding(12) };tabs.TabPages.Add(p);return p;
    }
    void InitializeFeatures(TableLayoutPanel outer,Control console)
    {
        outer.Controls.Remove(console);
        var tabs=new WorkspaceTabs { Dock=DockStyle.Fill,Font=new Font("Yu Gothic UI",10) };
        outer.Controls.Add(tabs,0,4);
        var dashboard=Page(tabs,"メトリクス");dashboard.AutoScroll=true;
        var dash=new TableLayoutPanel { Dock=DockStyle.Top,Height=480,ColumnCount=1,RowCount=4,MinimumSize=new Size(0,480) };dashboard.AutoScrollMinSize=new Size(0,480);dashboard.Resize+=delegate{dash.Height=Math.Max(480,dashboard.ClientSize.Height-8);};
        dash.RowStyles.Add(new RowStyle(SizeType.Absolute,94));dash.RowStyles.Add(new RowStyle(SizeType.Percent,100));dash.RowStyles.Add(new RowStyle(SizeType.Absolute,30));dash.RowStyles.Add(new RowStyle(SizeType.Absolute,40));dashboard.Controls.Add(dash);
        var meters=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=7,RowCount=1 };
        for(int i=0;i<7;i++)meters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/7));
        cpuValue=Metric(meters,"CPU / PC全体比",0);ramValue=Metric(meters,"RAM / プロセス",1);tpsValue=Metric(meters,"TPS / 直近5秒",2);msptValue=Metric(meters,"MSPT / 中央値",3);uptimeValue=Metric(meters,"稼働時間",4);onlineValue=Metric(meters,"オンライン",5);diskValue=Metric(meters,"ディスク空き",6);dash.Controls.Add(meters,0,0);
        uptimeValue.Font=new Font("Segoe UI",13,FontStyle.Bold);
        history=new Chart { Dock=DockStyle.Fill,BackColor=Card,BorderlineColor=Muted };
        var area=new ChartArea("usage") { BackColor=Card };
        area.AxisX.LabelStyle.ForeColor=Muted;area.AxisX.MajorGrid.Enabled=false;area.AxisX.LabelStyle.Format="HH:mm:ss";
        area.AxisY.LabelStyle.ForeColor=Accent;area.AxisY.Title="CPU %";area.AxisY.TitleForeColor=Accent;area.AxisY.Minimum=0;area.AxisY.Maximum=100;area.AxisY.MajorGrid.LineColor=Color.FromArgb(48,58,73);
        area.AxisY2.Enabled=AxisEnabled.True;area.AxisY2.LabelStyle.ForeColor=Color.CornflowerBlue;area.AxisY2.Title="RAM GiB";area.AxisY2.TitleForeColor=Color.CornflowerBlue;area.AxisY2.Minimum=0;area.AxisY2.MajorGrid.Enabled=false;
        history.ChartAreas.Add(area);
        history.Series.Add(new Series("CPU %") { ChartType=SeriesChartType.FastLine,Color=Accent,BorderWidth=2,XValueType=ChartValueType.DateTime });
        history.Series.Add(new Series("RAM GiB") { ChartType=SeriesChartType.FastLine,Color=Color.CornflowerBlue,BorderWidth=2,YAxisType=AxisType.Secondary,XValueType=ChartValueType.DateTime });
        history.Legends.Add(new Legend { BackColor=Card,ForeColor=Ink,Docking=Docking.Top });dash.Controls.Add(history,0,1);
        metricsNote=new Label { Text="CPU・RAMは1秒ごと、TPS・MSPT・参加者は15秒ごと。停止中や未取得の値は — と表示します。",Dock=DockStyle.Fill,ForeColor=Muted,AutoEllipsis=true };dash.Controls.Add(metricsNote,0,2);
        var metricActions=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false };
        FlowBtn(metricActions,"今すぐ取得",delegate { PollMetrics(); });FlowBtn(metricActions,"CSVを書き出す",delegate { ExportMetrics(); });FlowBtn(metricActions,"診断を表示",delegate { Send("spark health show --memory");tabs.SelectedIndex=1; });
        dash.Controls.Add(metricActions,0,3);
        var consolePage=Page(tabs,"コンソール");
        var consoleWrap=new TableLayoutPanel { Dock=DockStyle.Fill,RowCount=2,ColumnCount=1 };consoleWrap.RowStyles.Add(new RowStyle(SizeType.Absolute,42));consoleWrap.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var consoleTools=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false };searchText=Input(consoleTools,230);
        FlowBtn(consoleTools,"ログ内を検索",delegate { int pos=log.Text.IndexOf(searchText.Text,log.SelectionStart+log.SelectionLength,StringComparison.OrdinalIgnoreCase);if(pos<0)pos=log.Text.IndexOf(searchText.Text,StringComparison.OrdinalIgnoreCase);if(pos>=0){log.Select(pos,searchText.Text.Length);log.ScrollToCaret();log.Focus();} });
        FlowBtn(consoleTools,"ログを保存",delegate { ExportLog(); });FlowBtn(consoleTools,"ログフォルダー",delegate { Open(Path.Combine(ServerDir,"logs")); });
        consoleWrap.Controls.Add(consoleTools,0,0);console.Dock=DockStyle.Fill;consoleWrap.Controls.Add(console,0,1);consolePage.Controls.Add(consoleWrap);
        var usersPage=Page(tabs,"参加者・権限");
        var userLayout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3 };userLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));userLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));userLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));usersPage.Controls.Add(userLayout);
        usersGrid=Grid();usersGrid.Columns.Add("name","ユーザー名");usersGrid.Columns.Add("online","接続中");usersGrid.Columns.Add("allowed","参加許可");usersGrid.Columns.Add("op","OP権限");usersGrid.Columns.Add("banned","BAN");userLayout.Controls.Add(usersGrid,0,0);
        var userActions=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(0,6,0,0) };
        FlowBtn(userActions,"一覧更新",delegate { SendQuiet("list");RefreshUsers(); });FlowBtn(userActions,"OP付与",delegate { PlayerAction("op",true); });FlowBtn(userActions,"OP解除",delegate { PlayerAction("deop",true); });FlowBtn(userActions,"キック",delegate { PlayerAction("kick",true); });FlowBtn(userActions,"BAN",delegate { PlayerAction("ban",true); });FlowBtn(userActions,"BAN解除",delegate { PlayerAction("pardon",true); });userLayout.Controls.Add(userActions,0,1);
        var announce=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(0,6,0,0) };broadcastText=Input(announce,600);FlowBtn(announce,"全員へお知らせ",delegate { string s=broadcastText.Text.Trim();if(s.Length>0)Send("say "+s.Replace("\r","").Replace("\n"," ")); });userLayout.Controls.Add(announce,0,2);
        var settingsPage=Page(tabs,"サーバー設定");
        var form=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true };settingsPage.Controls.Add(form);
        motdSetting=TextSetting(form,"サーバー紹介",500);difficultySetting=ChoiceSetting(form,"難易度",new[]{"peaceful","easy","normal","hard"});modeSetting=ChoiceSetting(form,"ゲームモード",new[]{"survival","creative","adventure","spectator"});maxPlayersSetting=NumberSetting(form,"最大人数",1,100,10);viewSetting=NumberSetting(form,"描画距離 / chunks",2,32,8);simulationSetting=NumberSetting(form,"シミュレーション距離",2,32,6);
        pvpSetting=CheckSetting(form,"PvPを許可");flightSetting=CheckSetting(form,"飛行を許可（飛行系MOD利用時）");
        var settingsActions=new FlowLayoutPanel { AutoSize=true,WrapContents=false };FlowBtn(settingsActions,"設定を読み直す",delegate { LoadServerSettings(); });FlowBtn(settingsActions,"設定を保存",delegate { ApplyServerSettings(); });FlowBtn(settingsActions,"設定ファイルを開く",delegate { Open(Path.Combine(ServerDir,"server.properties")); });form.Controls.Add(settingsActions);
        form.Controls.Add(new Label { Text="設定の保存はサーバー停止中に行い、次回起動から適用します。オンライン認証と許可リストは既存サーバーの設定を確認してください。",AutoSize=true,ForeColor=Muted,MaximumSize=new Size(950,0) });
        var backupsPage=Page(tabs,"バックアップ・復元");
        var backupLayout=new TableLayoutPanel { Dock=DockStyle.Fill,RowCount=4,ColumnCount=1 };backupLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,55));backupLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));backupLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));backupLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));backupsPage.Controls.Add(backupLayout);
        var nativeBackup=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false };nativeBackup.Controls.Add(new Label{Text="ワールド自動保存",AutoSize=true,Margin=new Padding(0,8,10,0)});backupHours=new NumericUpDown{Minimum=1,Maximum=24,Value=2,Width=60,BackColor=Card,ForeColor=Ink};nativeBackup.Controls.Add(backupHours);nativeBackup.Controls.Add(new Label{Text="時間ごと / 保持",AutoSize=true,Margin=new Padding(8,8,8,0)});backupKeep=new NumericUpDown{Minimum=1,Maximum=100,Value=7,Width=60,BackColor=Card,ForeColor=Ink};nativeBackup.Controls.Add(backupKeep);FlowBtn(nativeBackup,"自動設定を保存",delegate { ApplyAutoBackup(); });FlowBtn(nativeBackup,"今すぐワールド保存",delegate { Send("backup start"); });backupLayout.Controls.Add(nativeBackup,0,0);
        backupLayout.Controls.Add(new Label{Text="自動保存は同梱のFTB Backupsで実行。下のZIPは管理アプリの完全バックアップです。復元前のサーバーはrecoveryフォルダーへ保管します。",Dock=DockStyle.Fill,ForeColor=Muted},0,1);
        backupsGrid=Grid();backupsGrid.Columns.Add("path","ファイル名");backupsGrid.Columns.Add("date","作成日時");backupsGrid.Columns.Add("size","サイズ MiB");backupLayout.Controls.Add(backupsGrid,0,2);
        var backupActions=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false };backup=FlowBtn(backupActions,"完全バックアップ",delegate { Backup(); });FlowBtn(backupActions,"一覧更新",delegate { RefreshBackups(); });FlowBtn(backupActions,"選択ZIPを復元",delegate { RestoreSelected(); });FlowBtn(backupActions,"自動保存先を開く",delegate { string d=Path.Combine(ServerDir,"backups");Directory.CreateDirectory(d);Open(d); });backupLayout.Controls.Add(backupActions,0,3);
        var automationPage=Page(tabs,"運用・スケジュール");var automation=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};automationPage.Controls.Add(automation);
        autoCrashRestart=CheckSetting(automation,"異常終了時に自動再起動（1回の起動につき最大3回）");scheduledRestart=CheckSetting(automation,"毎日指定時刻に再起動（管理アプリ起動中のみ）");restartTime=new DateTimePicker {Format=DateTimePickerFormat.Custom,CustomFormat="HH:mm",ShowUpDown=true,Width=120,Value=DateTime.Today.AddHours(5)};automation.Controls.Add(restartTime);
        var autoActions=new FlowLayoutPanel {AutoSize=true};FlowBtn(autoActions,"運用設定を保存",delegate { SaveFeatureSettings();Add("[運用] 設定を保存しました。"); });FlowBtn(autoActions,"保存して再起動",delegate { RequestRestart(); });FlowBtn(autoActions,"全部停止",delegate { restartPending=false;StopTunnel();StopServer(); });automation.Controls.Add(autoActions);
        automation.Controls.Add(new Label{Text="再起動はワールド保存とプロセス終了を待ちます。予定再起動は60秒前にプレイヤーへ通知します。\r\nアプリ終了中・PCスリープ中は実行されません。異常終了時の自動再起動は初期状態で無効です。",AutoSize=true,ForeColor=Muted,MaximumSize=new Size(950,0)});
        var modsPage=Page(tabs,"MOD・導入案内");var modsLayout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};modsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,68));modsLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));modsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));modsPage.Controls.Add(modsLayout);
        modsLayout.Controls.Add(new Label{Text="導入済みのMODとプラグインを表示します。Vanillaでは空欄になります。\r\n拡張機能の変更前に完全バックアップを作成し、クライアントとの互換性を確認してください。",Dock=DockStyle.Fill,ForeColor=Muted},0,0);
        modsGrid=Grid();modsGrid.Columns.Add("name","MOD / プラグインファイル");modsGrid.Columns.Add("size","サイズ MiB");modsLayout.Controls.Add(modsGrid,0,1);var modActions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};FlowBtn(modActions,"一覧更新",delegate {RefreshMods();});FlowBtn(modActions,"サーバーフォルダー",delegate {Open(ServerDir);});FlowBtn(modActions,"参加者向け案内",delegate {workspace.SelectedIndex=7;});modsLayout.Controls.Add(modActions,0,2);
        LoadServerSettings();RefreshUsers();RefreshMods();RefreshBackups();LoadAutoBackup();
    }
    Label Metric(TableLayoutPanel panel,string title,int column)
    {
        var card=new TableLayoutPanel{Dock=DockStyle.Fill,BackColor=Card,RowCount=2,ColumnCount=1,Margin=new Padding(0,0,6,8),Padding=new Padding(8)};card.RowStyles.Add(new RowStyle(SizeType.Absolute,24));card.RowStyles.Add(new RowStyle(SizeType.Percent,100));card.Controls.Add(new Label{Text=title,Dock=DockStyle.Fill,ForeColor=Muted,Font=new Font("Yu Gothic UI",9)},0,0);var value=new Label{Text="—",Dock=DockStyle.Fill,ForeColor=Accent,Font=new Font("Segoe UI",17,FontStyle.Bold)};card.Controls.Add(value,0,1);panel.Controls.Add(card,column,0);return value;
    }
    DataGridView Grid() { return new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Card,BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false,ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=Card,ForeColor=Ink},DefaultCellStyle=new DataGridViewCellStyle{BackColor=Bg,ForeColor=Ink,SelectionBackColor=Color.FromArgb(50,90,94),SelectionForeColor=Color.White}}; }
    TextBox Input(Control p,int width) {var t=new TextBox{Width=width,BackColor=Card,ForeColor=Ink,Margin=new Padding(0,5,10,0)};p.Controls.Add(t);return t;}
    FlowLayoutPanel SettingRow(Control p,string title) {var row=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Margin=new Padding(0,0,0,6)};row.Controls.Add(new Label{Text=title,Width=205,Margin=new Padding(0,5,6,0)});p.Controls.Add(row);return row;}
    TextBox TextSetting(Control p,string title,int width) {return Input(SettingRow(p,title),width);}
    ComboBox ChoiceSetting(Control p,string title,string[] values) {var c=new ComboBox{Width=180,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Card,ForeColor=Ink};c.Items.AddRange(values);SettingRow(p,title).Controls.Add(c);return c;}
    NumericUpDown NumberSetting(Control p,string title,int min,int max,int val) {var n=new NumericUpDown{Width=100,Minimum=min,Maximum=max,Value=val,BackColor=Card,ForeColor=Ink};SettingRow(p,title).Controls.Add(n);return n;}
    CheckBox CheckSetting(Control p,string title) {var c=new CheckBox{Text=title,AutoSize=true,Margin=new Padding(0,4,0,8)};p.Controls.Add(c);return c;}
    void ResetMetrics() {lock(metricLock){tps=mspt=double.NaN;lastTps=lastMspt=DateTime.MinValue;}lastSample=lastPoll=DateTime.MinValue;lastCpu=TimeSpan.Zero;cpuPercent=ramGiB=0;onlineCount=0;onlineNames="";expectTps=expectMspt=false;readyNotified=false;}
    void ParseServerMetrics(string line)
    {
        string text=Regex.Replace(line,@"^.*?\]:\s*","");
        var list=Regex.Match(text,@"There are (\d+) of a max of \d+ players online:\s*(.*)");if(list.Success){onlineCount=int.Parse(list.Groups[1].Value);onlineNames=list.Groups[2].Value;}
        bool tpsHeader=text.IndexOf("TPS from last",StringComparison.OrdinalIgnoreCase)>=0;
        bool msptHeader=text.IndexOf("Tick durations",StringComparison.OrdinalIgnoreCase)>=0;
        if(tpsHeader){expectTps=true;return;}if(msptHeader){expectMspt=true;return;}
        if(line.IndexOf("spark",StringComparison.OrdinalIgnoreCase)<0)return;
        string payload=Regex.Replace(text,@"^\[[^\]]+\]\s*","").Trim();
        var tpsRow=Regex.Match(payload,@"^\*?(\d+(?:\.\d+)?),\s*\*?\d");
        var msptRow=Regex.Match(payload,@"^\d+(?:\.\d+)?/(\d+(?:\.\d+)?)/\d");
        if(expectTps && tpsRow.Success) {double value;if(double.TryParse(tpsRow.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&value<=20.1){lock(metricLock){tps=value;lastTps=DateTime.UtcNow;}expectTps=false;}}
        if(expectMspt && msptRow.Success) {double value;if(double.TryParse(msptRow.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out value)){lock(metricLock){mspt=value;lastMspt=DateTime.UtcNow;}expectMspt=false;}}
    }
    void SendQuiet(string cmd) {if(Running(server)&&ready&&!stopping){try{server.StandardInput.WriteLine(cmd);server.StandardInput.Flush();}catch(Exception e){Add("[取得] "+e.Message);}}}
    void PollMetrics(){if(SparkAvailable)SendQuiet("spark tps");SendQuiet("list");lastPoll=DateTime.UtcNow;}
    void UpdateFeatures(bool sr,bool tr)
    {
        if(cpuValue==null)return;
        if(previousRunning&&!sr){bool expected=stopping;NotifyDiscord("lifecycle",expected?"サーバー停止":"サーバー異常終了",expected?"ワールドを保存して停止しました。":"サーバープロセスが予期せず終了しました。主催者は管理アプリのログを確認してください。");ready=false;stopping=false;RefreshUsers();RefreshBackups();if(restartPending&&!closing){restartPending=false;StartServer();sr=true;}else if(!expected&&!closing&&autoCrashRestart.Checked&&crashAttempts<3){crashAttempts++;Add("[運用] 異常終了を検出。自動再起動 "+crashAttempts+"/3");StartServer();sr=true;}}
        if(sr&&ready&&!stopping&&!readyNotified){readyNotified=true;NotifyDiscord("lifecycle","サーバー起動完了","サーバーの準備ができました。公開接続先は主催者の案内を確認してください。");}
        previousRunning=sr;
        if(scheduledRestart.Checked&&sr&&ready&&!stopping&&lastScheduled.Date!=DateTime.Today){DateTime target=DateTime.Today.Add(restartTime.Value.TimeOfDay);double seconds=(target-DateTime.Now).TotalSeconds;if(seconds>=0&&seconds<=60){lastScheduled=DateTime.Today;Send("say [Server] 60秒後にサーバーを再起動します。");Task.Delay(TimeSpan.FromSeconds(Math.Max(1,seconds))).ContinueWith(delegate { if(!IsDisposed&&IsHandleCreated)BeginInvoke((Action)delegate {if(scheduledRestart.Checked&&!closing)RequestRestart();}); });}}
        if((DateTime.UtcNow-lastSample).TotalSeconds<1)return;
        DateTime now=DateTime.UtcNow;double elapsed=lastSample==DateTime.MinValue?0:(now-lastSample).TotalSeconds;lastSample=now;
        RefreshExperience();
        try{
            if(sr){server.Refresh();TimeSpan cpu=server.TotalProcessorTime;cpuPercent=elapsed>0?Math.Max(0,Math.Min(100,(cpu-lastCpu).TotalSeconds/elapsed/Environment.ProcessorCount*100)):0;lastCpu=cpu;ramGiB=server.WorkingSet64/1073741824.0;cpuValue.Text=cpuPercent.ToString("0.0")+" %";ramValue.Text=ramGiB.ToString("0.00")+" GiB";uptimeValue.Text=(DateTime.Now-server.StartTime).ToString(@"dd\.hh\:mm\:ss");onlineValue.Text=ready?onlineCount.ToString():"—";
                lock(metricLock){tpsValue.Text=(ready&&(now-lastTps).TotalSeconds<45&&!double.IsNaN(tps))?tps.ToString("0.0"):"—";msptValue.Text=(ready&&(now-lastMspt).TotalSeconds<45&&!double.IsNaN(mspt))?mspt.ToString("0.0")+" ms":"—";tpsValue.ForeColor=tps<18?Color.Salmon:Accent;msptValue.ForeColor=mspt>50?Color.Salmon:Accent;}
                history.Series[0].Points.AddXY(DateTime.Now.ToOADate(),cpuPercent);history.Series[1].Points.AddXY(DateTime.Now.ToOADate(),ramGiB);while(history.Series[0].Points.Count>300){history.Series[0].Points.RemoveAt(0);history.Series[1].Points.RemoveAt(0);}
                var inv=CultureInfo.InvariantCulture;samples.Add(now.ToString("o")+","+cpuPercent.ToString(inv)+","+ramGiB.ToString(inv)+","+(tpsValue.Text=="—"?"":tps.ToString(inv))+","+(msptValue.Text=="—"?"":mspt.ToString(inv))+","+onlineCount);if(samples.Count>86400)samples.RemoveAt(0);
                if(!SparkAvailable){tpsValue.Text=msptValue.Text="未導入";metricsNote.Text="CPU・RAMは実測値。TPS・MSPTはspark未検出のため表示しません。内蔵版はサーバー設定でenabledを選べます。";}else metricsNote.Text="CPU・RAMは1秒、TPS・MSPT・人数は15秒ごとに取得。未取得・古い値は — と表示します。";
                if(ready&&!stopping&&(now-lastPoll).TotalSeconds>=15){PollMetrics();RefreshUsers();}
            }else{cpuValue.Text=ramValue.Text=tpsValue.Text=msptValue.Text=uptimeValue.Text=onlineValue.Text="—";}
            diskValue.Text=(new DriveInfo(Path.GetPathRoot(root)).AvailableFreeSpace/1073741824.0).ToString("0.0")+" GiB";
        }catch(Exception e){metricsNote.Text="メトリクス取得待ち："+e.Message;}
    }
    void RequestRestart(){if(!Running(server)){StartServer();return;}if(stopping)return;restartPending=true;StopServer();Add("[運用] 保存・停止後に再起動します。");}
    void ExportMetrics(){using(var d=new SaveFileDialog{Filter="CSV|*.csv",FileName="astral-metrics-"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".csv"}){if(d.ShowDialog()!=DialogResult.OK)return;File.WriteAllLines(d.FileName,new[]{"timestamp_utc,cpu_pc_percent,process_ram_gib,tps_5s,mspt_median,players"}.Concat(samples),new UTF8Encoding(true));Add("[管理] CSVを保存しました。");}}
    void ExportLog(){using(var d=new SaveFileDialog{Filter="Text|*.txt",FileName="astral-log-"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".txt"}){if(d.ShowDialog()!=DialogResult.OK)return;File.WriteAllText(d.FileName,Regex.Replace(log.Text,@"https://playit\.gg/claim/\w+","[claim URL hidden]"),new UTF8Encoding(true));}}
    void RefreshUsers()
    {
        if(usersGrid==null)return;var users=new SortedDictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
        foreach(string n in onlineNames.Split(new[]{','},StringSplitOptions.RemoveEmptyEntries)){string name=n.Trim();users[name]=new[]{name,"●","","",""};}
        string[] files={"whitelist.json","ops.json","banned-players.json"};for(int i=0;i<files.Length;i++){string path=Path.Combine(ServerDir,files[i]);if(!File.Exists(path))continue;try{var arr=new JavaScriptSerializer().Deserialize<List<Dictionary<string,object>>>(File.ReadAllText(path));foreach(var row in arr){object obj;if(!row.TryGetValue("name",out obj)||obj==null)continue;string name=obj.ToString();if(!users.ContainsKey(name))users[name]=new[]{name,"","","",""};users[name][i+2]="●";}}catch(Exception){}}
        usersGrid.Rows.Clear();foreach(var u in users.Values)usersGrid.Rows.Add(u);
    }
    void PlayerAction(string action,bool confirm)
    {
        string n=usersGrid.SelectedRows.Count>0?Convert.ToString(usersGrid.SelectedRows[0].Cells[0].Value):player.Text.Trim();if(!Regex.IsMatch(n,@"^[A-Za-z0-9_]{1,16}$"))throw new Exception("参加者一覧でユーザーを選択するか、下の欄に名前を入力してください。");if(confirm&&MessageBox.Show(n+" に「"+action+"」を実行しますか？","参加者操作",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;Send(action+" "+n);
    }
    Dictionary<string,string> ReadProperties(){var d=new Dictionary<string,string>{{"motd","Minecraft Server"},{"difficulty","normal"},{"gamemode","survival"},{"max-players","20"},{"view-distance","8"},{"simulation-distance","6"},{"pvp","true"},{"allow-flight","false"},{"level-name","world"},{"server-port","25565"}};foreach(string line in File.ReadAllLines(Path.Combine(ServerDir,"server.properties"))){if(line.StartsWith("#"))continue;int n=line.IndexOf('=');if(n>0)d[line.Substring(0,n)]=line.Substring(n+1).TrimEnd();}return d;}
    void LoadServerSettings(){if(motdSetting==null)return;var d=ReadProperties();motdSetting.Text=d["motd"];difficultySetting.SelectedItem=d["difficulty"];modeSetting.SelectedItem=d["gamemode"];SetNumber(maxPlayersSetting,d["max-players"]);SetNumber(viewSetting,d["view-distance"]);SetNumber(simulationSetting,d["simulation-distance"]);pvpSetting.Checked=d["pvp"]=="true";flightSetting.Checked=d["allow-flight"]=="true";}
    void SetNumber(NumericUpDown c,string s){decimal v;if(decimal.TryParse(s,out v))c.Value=Math.Max(c.Minimum,Math.Min(c.Maximum,v));}
    void EnsureStopped(){if(Running(server)||backupBusy)throw new Exception("サーバー停止・バックアップ完了後に操作してください。");using(var s=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)){s.ExclusiveAddressUse=true;s.Bind(new IPEndPoint(IPAddress.Loopback,ServerPort));}}
    void ApplyServerSettings()
    {
        EnsureStopped();string[] values={motdSetting.Text.Trim().Replace("\r","").Replace("\n",""),Convert.ToString(difficultySetting.SelectedItem),Convert.ToString(modeSetting.SelectedItem),maxPlayersSetting.Value.ToString(),viewSetting.Value.ToString(),simulationSetting.Value.ToString(),pvpSetting.Checked?"true":"false",flightSetting.Checked?"true":"false"};string path=Path.Combine(ServerDir,"server.properties"),text=File.ReadAllText(path);File.Copy(path,path+".manager.bak",true);for(int i=0;i<configKeys.Length;i++){string pattern="(?m)^"+Regex.Escape(configKeys[i])+"=.*$";string line=configKeys[i]+"="+EscapeProperty(values[i]);if(Regex.IsMatch(text,pattern))text=Regex.Replace(text,pattern,delegate(Match m){return line;});else text=text.TrimEnd()+Environment.NewLine+line+Environment.NewLine;}File.WriteAllText(path,text,new UTF8Encoding(false));Add("[設定] 保存しました。次回起動から適用されます。");
    }
    static string EscapeProperty(string value){var s=new StringBuilder();foreach(char c in value){if(c>127)s.Append("\\u"+((int)c).ToString("x4"));else s.Append(c);}return s.ToString();}
    void LoadAutoBackup(){string path=Path.Combine(ServerDir,"config","ftbbackups2.json");if(!File.Exists(path))return;string text=File.ReadAllText(path);var h=Regex.Match(text,"\"backup_cron\"\\s*:\\s*\"0 0 \\*/(\\d+)");var k=Regex.Match(text,"\"max_backups\"\\s*:\\s*(\\d+)");if(h.Success)SetNumber(backupHours,h.Groups[1].Value);if(k.Success)SetNumber(backupKeep,k.Groups[1].Value);}
    void ApplyAutoBackup()
    {
        EnsureStopped();string path=Path.Combine(ServerDir,"config","ftbbackups2.json"),text=File.ReadAllText(path);File.Copy(path,path+".manager.bak",true);text=Regex.Replace(text,"\"enabled\"\\s*:\\s*(true|false)","\"enabled\": true");text=Regex.Replace(text,"\"max_backups\"\\s*:\\s*\\d+","\"max_backups\": "+backupKeep.Value);text=Regex.Replace(text,"\"backup_cron\"\\s*:\\s*\"[^\"]*\"","\"backup_cron\": \"0 0 */"+backupHours.Value+" * * ?\"");text=Regex.Replace(text,"\"minimum_free_space\"\\s*:\\s*\\d+","\"minimum_free_space\": 2048");File.WriteAllText(path,text,new UTF8Encoding(false));Add("[設定] 自動ワールドバックアップを保存。次回起動時に反映します。");
    }
    void SaveFeatureSettings(){if(autoCrashRestart==null)return;var d=new Dictionary<string,object>{{"autoCrashRestart",autoCrashRestart.Checked},{"scheduledRestart",scheduledRestart.Checked},{"restartTime",restartTime.Value.ToString("HH:mm")},{"autoMemory",autoMemory==null||autoMemory.Checked},{"largeText",largeText!=null&&largeText.Checked},{"highContrast",highContrast!=null&&highContrast.Checked}};File.WriteAllText(FeatureSettings,new JavaScriptSerializer().Serialize(d));}
    void LoadFeatureSettings(){if(File.Exists(FeatureSettings))try{var d=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(FeatureSettings));autoCrashRestart.Checked=Convert.ToBoolean(d["autoCrashRestart"]);scheduledRestart.Checked=Convert.ToBoolean(d["scheduledRestart"]);DateTime time;if(DateTime.TryParse(Convert.ToString(d["restartTime"]),out time))restartTime.Value=DateTime.Today.Add(time.TimeOfDay);if(d.ContainsKey("autoMemory"))autoMemory.Checked=Convert.ToBoolean(d["autoMemory"]);if(d.ContainsKey("largeText"))largeText.Checked=Convert.ToBoolean(d["largeText"]);if(d.ContainsKey("highContrast"))highContrast.Checked=Convert.ToBoolean(d["highContrast"]);}catch(Exception e){Add("[運用設定] "+e.Message);}ApplyAccessibleTheme();RefreshExperience();}
    void RefreshMods(){modsGrid.Rows.Clear();foreach(string folder in new[]{"mods","plugins"}){string dir=Path.Combine(ServerDir,folder);if(!Directory.Exists(dir))continue;foreach(string p in Directory.GetFiles(dir,"*.jar").OrderBy(x=>x))modsGrid.Rows.Add(folder+"/"+Path.GetFileName(p),(new FileInfo(p).Length/1048576.0).ToString("0.0"));}}
    void RefreshBackups(){if(backupsGrid==null)return;Directory.CreateDirectory(Path.Combine(root,"backups"));backupsGrid.Rows.Clear();foreach(var f in new DirectoryInfo(Path.Combine(root,"backups")).GetFiles("*.zip").OrderByDescending(f=>f.LastWriteTime))backupsGrid.Rows.Add(f.Name,f.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),(f.Length/1048576.0).ToString("0.0"));}
    void RestoreSelected()
    {
        EnsureStopped();if(backupsGrid.SelectedRows.Count==0)throw new Exception("復元する完全バックアップを選択してください。");string name=Convert.ToString(backupsGrid.SelectedRows[0].Cells[0].Value);if(Path.GetFileName(name)!=name)throw new Exception("無効なファイル名です。");string zip=Path.Combine(root,"backups",name);
        if(MessageBox.Show(name+" を復元します。現在のサーバーはrecoveryフォルダーへ移して保管します。","復元",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK)return;backupBusy=true;Add("[復元] ZIPを検証・展開しています…");Task.Run(delegate{try{string stage=Path.Combine(root,"restore-stage-"+Guid.NewGuid().ToString("N"));ExtractValidated(zip,stage);string recovery=Path.Combine(root,"recovery","server-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(Path.GetDirectoryName(recovery));Directory.Move(ServerDir,recovery);try{Directory.Move(stage,ServerDir);}catch{Directory.Move(recovery,ServerDir);throw;}Add("[復元] 完了。以前のデータは "+recovery+" に保管しました。");NotifyDiscord("backup","復元完了","完全バックアップからサーバーを復元しました。");}catch(Exception e){Add("[復元失敗] "+e.Message);NotifyDiscord("backup","復元失敗","復元処理が完了しませんでした。主催者は管理アプリのログを確認してください。");}finally{backupBusy=false;}});
    }
    static void ExtractValidated(string zip,string stage)
    {
        string prefix=Path.GetFullPath(stage)+Path.DirectorySeparatorChar;Directory.CreateDirectory(stage);
        using(var a=ZipFile.OpenRead(zip)){long total=0;foreach(var e in a.Entries){string n=e.FullName.Replace('\\','/');string target=Path.GetFullPath(Path.Combine(stage,n));if(!target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("ZIPに不正なパスが含まれています。");total=checked(total+e.Length);}
            if(new DriveInfo(Path.GetPathRoot(stage)).AvailableFreeSpace<total+1073741824L)throw new IOException("復元に必要なディスク空き容量がありません。");
            foreach(var e in a.Entries){string n=e.FullName.Replace('\\','/');string target=Path.GetFullPath(Path.Combine(stage,n));if(n.EndsWith("/")){Directory.CreateDirectory(target);continue;}Directory.CreateDirectory(Path.GetDirectoryName(target));e.ExtractToFile(target,false);}}
        if(!File.Exists(Path.Combine(stage,"server.properties")))throw new InvalidDataException("サーバー設定が含まれないバックアップです。");if(Directory.GetFiles(stage,"*.jar").Length==0&&!Directory.Exists(Path.Combine(stage,"libraries")))throw new InvalidDataException("サーバーの起動ファイルが含まれません。");
    }
    void RunFeatureChecks()
    {
        string scratch=Path.Combine(Path.GetTempPath(),"AstralManagerChecks",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(scratch);
        string backup=Directory.GetFiles(Path.Combine(root,"backups"),"*.zip").First();string stage=Path.Combine(scratch,"restore-valid");ExtractValidated(backup,stage);
        if(!File.Exists(Path.Combine(stage,"whitelist.json")))throw new Exception("Whitelist not restored");
        string invalid=Path.Combine(scratch,"traversal.zip");using(var zip=ZipFile.Open(invalid,ZipArchiveMode.Create)){using(var w=new StreamWriter(zip.CreateEntry("../escape.txt").Open()))w.Write("must not escape");}
        bool blocked=false;try{ExtractValidated(invalid,Path.Combine(scratch,"restore-invalid"));}catch(InvalidDataException){blocked=true;}
        if(!blocked||File.Exists(Path.Combine(scratch,"escape.txt")))throw new Exception("Unsafe archive not rejected");
        if(usersGrid==null||modsGrid==null||history.Series.Count!=2)throw new Exception("Feature initialization incomplete");
    }
    static int ReplayMetrics()
    {
        var manager=new AstralManager();
        foreach(string line in File.ReadAllLines(Path.Combine(manager.root,"verification-server.log")))if(line.StartsWith("[MC] "))manager.ParseServerMetrics(line.Substring(5));
        bool ok=!double.IsNaN(manager.tps)&&!double.IsNaN(manager.mspt);
        File.WriteAllText(Path.Combine(manager.root,"metrics-parser-verification.txt"),(ok?"PASS":"FAIL")+": actual spark output replay, TPS="+manager.tps+", MSPT="+manager.mspt);
        manager.timer.Stop();manager.Dispose();return ok?0:1;
    }
}
