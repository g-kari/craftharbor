using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

class WorkspaceTabs : TabControl
{
    public WorkspaceTabs(){Appearance=TabAppearance.FlatButtons;ItemSize=new Size(0,1);SizeMode=TabSizeMode.Fixed;TabStop=false;}
    protected override void WndProc(ref Message m){if(m.Msg==0x1328){m.Result=(IntPtr)1;return;}base.WndProc(ref m);}
}

class ActionButton : Button
{
    public string IconKey="";
    public bool Selected,Navigation;
    bool hover;
    public ActionButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Cursor=Cursors.Hand;}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        Color bg=BackColor,fg=Enabled?ForeColor:Color.FromArgb(139,154,173);
        if(Selected)bg=Color.FromArgb(37,66,72);else if(hover&&Enabled)bg=Color.FromArgb(Math.Min(255,bg.R+14),Math.Min(255,bg.G+14),Math.Min(255,bg.B+14));
        using(var b=new SolidBrush(bg))e.Graphics.FillRectangle(b,ClientRectangle);
        if(Selected)using(var b=new SolidBrush(Color.FromArgb(111,218,192)))e.Graphics.FillRectangle(b,0,5,3,Height-10);
        int inset=IconKey.Length>0?38:12;
        if(IconKey.Length>0)Icons.Draw(e.Graphics,IconKey,new Rectangle(13,(Height-18)/2,18,18),fg);
        TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(inset,0,Math.Max(0,Width-inset-10),Height),fg,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        if(Focused){using(var p=new Pen(Color.FromArgb(241,215,120),2)){p.DashStyle=DashStyle.Dot;e.Graphics.DrawRectangle(p,3,3,Width-7,Height-7);}}
    }
}

static class Icons
{
    public static void Draw(Graphics g,string key,Rectangle r,Color c)
    {
        var state=g.Save();g.TranslateTransform(r.X,r.Y);g.ScaleTransform(r.Width/20f,r.Height/20f);g.SmoothingMode=SmoothingMode.AntiAlias;
        using(var p=new Pen(c,1.7f))using(var b=new SolidBrush(c)){
            p.StartCap=p.EndCap=LineCap.Round;
            switch(key){
                case "play":g.FillPolygon(b,new[]{new Point(5,3),new Point(17,10),new Point(5,17)});break;
                case "stop":g.FillRectangle(b,4,4,12,12);break;
                case "chart":g.DrawLines(p,new[]{new Point(3,3),new Point(3,17),new Point(18,17)});g.DrawLines(p,new[]{new Point(5,13),new Point(9,8),new Point(12,11),new Point(17,4)});break;
                case "console":g.DrawRectangle(p,2,3,16,14);g.DrawLines(p,new[]{new Point(5,7),new Point(8,10),new Point(5,13)});g.DrawLine(p,11,13,15,13);break;
                case "users":g.DrawEllipse(p,7,2,6,6);g.DrawArc(p,4,10,12,10,180,180);g.DrawArc(p,0,11,7,8,180,130);g.DrawArc(p,13,11,7,8,230,130);break;
                case "settings":g.DrawLine(p,3,5,17,5);g.DrawLine(p,3,10,17,10);g.DrawLine(p,3,15,17,15);g.FillRectangle(b,6,2,3,6);g.FillRectangle(b,12,7,3,6);g.FillRectangle(b,7,12,3,6);break;
                case "backup":g.DrawRectangle(p,3,3,14,14);g.DrawLine(p,3,8,17,8);g.DrawLine(p,7,3,7,8);g.DrawRectangle(p,7,11,6,6);break;
                case "clock":g.DrawEllipse(p,2,2,16,16);g.DrawLine(p,10,5,10,10);g.DrawLine(p,10,10,14,12);break;
                case "mods":g.DrawPolygon(p,new[]{new Point(10,2),new Point(18,6),new Point(18,14),new Point(10,18),new Point(2,14),new Point(2,6)});g.DrawLine(p,2,6,10,10);g.DrawLine(p,18,6,10,10);g.DrawLine(p,10,10,10,18);break;
                case "guide":g.DrawRectangle(p,4,2,12,16);g.DrawLine(p,7,6,13,6);g.DrawLine(p,7,10,13,10);g.DrawLine(p,7,14,11,14);break;
                case "link":g.DrawArc(p,2,7,11,7,30,290);g.DrawArc(p,7,3,11,7,210,290);g.DrawLine(p,7,12,13,7);break;
                case "folder":g.DrawLines(p,new[]{new Point(2,16),new Point(2,4),new Point(8,4),new Point(10,7),new Point(18,7),new Point(16,16),new Point(2,16)});break;
                case "copy":g.DrawRectangle(p,3,2,10,12);g.DrawRectangle(p,7,6,10,12);break;
                case "refresh":g.DrawArc(p,3,3,14,14,30,295);g.DrawLines(p,new[]{new Point(12,2),new Point(17,4),new Point(16,9)});break;
                default:g.DrawEllipse(p,4,4,12,12);break;
            }
        }g.Restore(state);
    }
}

partial class AstralManager
{
    CheckBox autoMemory,largeText,highContrast;
    Label heapPreview,heapExplanation,pageHeading,sideStatus;
    TextBox participantGuide;
    WorkspaceTabs workspace;
    readonly List<ActionButton> navigation=new List<ActionButton>();
    readonly Dictionary<Control,Font> baseFonts=new Dictionary<Control,Font>();
    readonly Dictionary<Control,Color[]> baseColors=new Dictionary<Control,Color[]>();
    readonly Dictionary<Control,int> baseWidths=new Dictionary<Control,int>();
    readonly ToolTip tips=new ToolTip { AutoPopDelay=12000,InitialDelay=500,ReshowDelay=200 };
    int activeHeapGiB;
    DateTime memoryPreviewAt=DateTime.MinValue;
    [StructLayout(LayoutKind.Sequential)]class MemoryStatus
    {
        public uint length=(uint)Marshal.SizeOf(typeof(MemoryStatus)),load;
        public ulong totalPhysical,availablePhysical,totalPageFile,availablePageFile,totalVirtual,availableVirtual,availableExtendedVirtual;
    }
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool GlobalMemoryStatusEx([In,Out]MemoryStatus status);
    static double[] PhysicalMemory(){var s=new MemoryStatus();if(!GlobalMemoryStatusEx(s))throw new InvalidOperationException("PCのメモリ情報を取得できませんでした。");return new[]{s.totalPhysical/1073741824.0,s.availablePhysical/1073741824.0};}
    static int AutoHeap(double total,double available)
    {
        // Leave both an OS budget and currently used memory intact. Only the Java heap maximum changes.
        double maximum=Math.Min(total-Math.Max(4,total*.08),available-Math.Max(2,total*.04));
        return Math.Max(0,(int)Math.Floor(maximum));
    }
    int ResolveHeapBudget()
    {
        int safe=CurrentSharedBudget(!autoMemory.Checked);if(safe<4)throw new InvalidOperationException("他サーバーの予約分を残すとメモリが足りません。上限は起動時に決まるため、既存サーバーを停止・再起動して再配分するか、割当を下げてください。");
        int target=autoMemory.Checked?safe:(int)memory.Value;
        if(target>safe)throw new InvalidOperationException("指定したメモリ "+target+" GiB に対し、現在の利用可能な予算は "+safe+" GiBです。自動モードを選ぶか、割り当てを下げてください。");
        return target;
    }
    void UpgradeExperience(TableLayoutPanel old,Control cards,Control join,Control oldHeading,Control oldTools,Control allow)
    {
        workspace=(WorkspaceTabs)old.Controls.Cast<Control>().First(c=>c is WorkspaceTabs);
        var metricCards=(TableLayoutPanel)cpuValue.Parent.Parent;var metricDashboard=(TableLayoutPanel)metricCards.Parent;
        var metricPanels=metricCards.Controls.Cast<Control>().ToArray();metricCards.Controls.Clear();metricCards.ColumnCount=4;metricCards.RowCount=2;metricCards.ColumnStyles.Clear();metricCards.RowStyles.Clear();for(int i=0;i<4;i++)metricCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));metricCards.RowStyles.Add(new RowStyle(SizeType.Percent,50));metricCards.RowStyles.Add(new RowStyle(SizeType.Percent,50));for(int i=0;i<metricPanels.Length;i++)metricCards.Controls.Add(metricPanels[i],i%4,i/4);metricDashboard.RowStyles[0].Height=188;
        Controls.Remove(old);
        var shell=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=Bg,Margin=new Padding(0),Padding=new Padding(0)};shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,208));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(shell);
        var sidebar=new TableLayoutPanel {Dock=DockStyle.Fill,BackColor=Color.FromArgb(13,19,28),ColumnCount=1,RowCount=4,Padding=new Padding(16,24,12,16)};sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute,132));sidebar.RowStyles.Add(new RowStyle(SizeType.Percent,100));sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute,96));sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute,50));shell.Controls.Add(sidebar,0,0);
        var brand=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};brand.Controls.Add(new Label{Text="A S T R A L",AutoSize=true,Font=new Font("Segoe UI",21,FontStyle.Bold),ForeColor=Accent});serverIdentity=new Label{Text=profile.Name,AutoSize=true,Margin=new Padding(0,12,0,0),ForeColor=Ink,MaximumSize=new Size(176,42)};brand.Controls.Add(serverIdentity);sidebar.Controls.Add(brand,0,0);
        var nav=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(0,8,0,0)};sidebar.Controls.Add(nav,0,1);
        AddParticipantPage();
        var serversButton=FlowBtn(brand,"サーバー一覧",delegate{OpenServersDialog();});serversButton.Width=176;((ActionButton)serversButton).IconKey="folder";serversButton.AccessibleDescription="別のサーバーの登録、切替、別ウィンドウでの起動ができます。";
        AddDiscordPage();
        string[] names={"ダッシュボード","コンソール","参加者・権限","サーバー設定","バックアップ","運用・予約","MOD・プラグイン","参加者向け案内","Discord通知"};string[] icons={"chart","console","users","settings","backup","clock","mods","guide","link"};
        for(int i=0;i<names.Length;i++){int index=i;var b=(ActionButton)Btn(nav,names[i],0,0,176,delegate{workspace.SelectedIndex=index;});b.Height=36;b.Margin=new Padding(0,0,0,3);b.Navigation=true;b.IconKey=icons[i];b.AccessibleName=names[i];b.AccessibleDescription=names[i]+"画面へ移動します。Alt+"+(i+1);navigation.Add(b);}
        var accessibility=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};accessibility.Controls.Add(new Label{Text="表示・アクセシビリティ",AutoSize=true,ForeColor=Muted,Margin=new Padding(0,0,0,8)});largeText=new CheckBox{Text="文字を大きく",AutoSize=true,Height=30,AccessibleName="文字を大きく表示"};highContrast=new CheckBox{Text="高コントラスト",AutoSize=true,Height=30,Checked=SystemInformation.HighContrast,AccessibleName="高コントラスト表示"};accessibility.Controls.Add(largeText);accessibility.Controls.Add(highContrast);sidebar.Controls.Add(accessibility,0,2);
        sideStatus=new Label{Text="LOCAL SERVER\n127.0.0.1 : "+ServerPort,Dock=DockStyle.Fill,ForeColor=Muted,Font=new Font("Segoe UI",9)};sidebar.Controls.Add(sideStatus,0,3);
        var main=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(24,24,24,20)};main.RowStyles.Add(new RowStyle(SizeType.Absolute,94));main.RowStyles.Add(new RowStyle(SizeType.Absolute,140));main.RowStyles.Add(new RowStyle(SizeType.Absolute,94));main.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.Controls.Add(main,1,0);
        var header=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,320));var titleGroup=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};pageHeading=new Label{Text=names[0],Font=new Font("Yu Gothic UI",23,FontStyle.Bold),AutoSize=true,ForeColor=Ink};titleGroup.Controls.Add(pageHeading);profileSummary=new Label{Text=ProfileMetadata(),AutoSize=true,ForeColor=Muted,MaximumSize=new Size(630,42)};titleGroup.Controls.Add(profileSummary);header.Controls.Add(titleGroup,0,0);
        var primary=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Padding=new Padding(0,6,0,0)};var go=FlowBtn(primary,"一括起動",delegate{StartServer();StartTunnel();});go.Width=145;go.Height=44;go.BackColor=Accent;go.ForeColor=Bg;((ActionButton)go).IconKey="play";var help=FlowBtn(primary,"参加者の案内",delegate{workspace.SelectedIndex=7;});help.Width=155;help.Height=44;((ActionButton)help).IconKey="guide";header.Controls.Add(primary,1,0);main.Controls.Add(header,0,0);
        cards.Dock=DockStyle.Fill;main.Controls.Add(cards,0,1);join.Dock=DockStyle.Fill;main.Controls.Add(join,0,2);workspace.Dock=DockStyle.Fill;main.Controls.Add(workspace,0,3);
        var publicCard=cards.Controls[1];var publicButtons=publicCard.Controls.Cast<Control>().Where(c=>c is Button).ToArray();var publicActions=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=64,Padding=new Padding(12,8,12,12),ColumnCount=3,RowCount=1};for(int i=0;i<3;i++)publicActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));for(int i=0;i<publicButtons.Length;i++){publicButtons[i].Dock=DockStyle.Fill;publicButtons[i].Margin=new Padding(0,0,6,0);publicActions.Controls.Add(publicButtons[i],i,0);}publicCard.Controls.Add(publicActions);connect.Text="開始";disconnect.Text="停止";foreach(Control c in publicButtons)if(c!=connect&&c!=disconnect)c.Text="設定";
        var joinRow=join.Controls.Cast<Control>().OfType<TableLayoutPanel>().First();joinRow.ColumnStyles[1].Width=110;joinRow.ColumnStyles[2].Width=100;joinRow.ColumnStyles[3].Width=170;
        var users=workspace.TabPages[2];allow.Dock=DockStyle.Bottom;allow.Height=50;users.Controls.Add(allow);users.Controls.SetChildIndex(allow,0);
        var local=cards.Controls[0];foreach(Control c in local.Controls.Cast<Control>().Where(c=>c is Label&&c.Text=="GB").ToArray())local.Controls.Remove(c);
        heapPreview=new Label{Text="メモリ予算を確認中",Location=new Point(16,100),AutoSize=true,ForeColor=Muted,Font=new Font("Yu Gothic UI",9),AccessibleName="サーバーメモリの割り当て状況"};local.Controls.Add(heapPreview);
        var serverForm=(FlowLayoutPanel)workspace.TabPages[3].Controls[0];
        var memoryGroup=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0,0,0,18)};memoryGroup.Controls.Add(new Label{Text="メモリ割り当て",AutoSize=true,Font=new Font("Yu Gothic UI",13,FontStyle.Bold)});autoMemory=new CheckBox{Text="自動（専用PC向け・初期値）",AutoSize=true,Checked=true,AccessibleName="サーバーメモリを自動調整",AccessibleDescription="起動時の物理メモリと空き容量から上限を決めます。OSの動作分を残します。"};memoryGroup.Controls.Add(autoMemory);
        var manualRow=new FlowLayoutPanel{AutoSize=true,WrapContents=false};manualRow.Controls.Add(new Label{Text="手動上限",Width=110,Margin=new Padding(0,4,8,0)});var info=PhysicalMemory();memory.Maximum=Math.Max(4,(int)Math.Floor(info[0]-Math.Max(4,info[0]*.08)));memory.Width=90;memory.Location=Point.Empty;manualRow.Controls.Add(memory);manualRow.Controls.Add(new Label{Text="GiB",AutoSize=true,Margin=new Padding(8,4,0,0)});memoryGroup.Controls.Add(manualRow);
        heapExplanation=new Label{AutoSize=true,MaximumSize=new Size(760,0),ForeColor=Muted};memoryGroup.Controls.Add(heapExplanation);serverForm.Controls.Add(memoryGroup);serverForm.Controls.SetChildIndex(memoryGroup,0);
        workspace.SelectedIndexChanged+=delegate{for(int i=0;i<navigation.Count;i++){navigation[i].Selected=i==workspace.SelectedIndex;navigation[i].AccessibleDescription=(i==workspace.SelectedIndex?"現在表示している画面。":"画面へ移動。")+"Alt+"+(i+1);navigation[i].Invalidate();}pageHeading.Text=names[workspace.SelectedIndex];if(workspace.SelectedIndex==7)RefreshGuide();};navigation[0].Selected=true;
        autoMemory.CheckedChanged+=delegate{memoryPreviewAt=DateTime.MinValue;RefreshExperience();};largeText.CheckedChanged+=delegate{ApplyAccessibleTheme();};highContrast.CheckedChanged+=delegate{ApplyAccessibleTheme();};
        KeyPreview=true;KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Alt&&e.KeyCode>=Keys.D1&&e.KeyCode<=Keys.D9){int n=e.KeyCode-Keys.D1;workspace.SelectedIndex=n;navigation[n].Focus();e.Handled=true;}if(e.Control&&e.KeyCode==Keys.L){workspace.SelectedIndex=1;command.Focus();e.SuppressKeyPress=true;}if(e.KeyCode==Keys.F1){workspace.SelectedIndex=7;e.Handled=true;}};
        AddProfileSettings();RefreshProfileLabels();
        StyleControls(this);
        tips.SetToolTip(autoMemory,"起動時に上限を計算。稼働中はJVMがその範囲で使用量を増減します。上限の再計算は次回起動時です。");tips.SetToolTip(start,"サーバーを起動。公開する場合はplayit.ggも開始してください。");tips.SetToolTip(stop,"ワールドを保存して停止します。停止完了まで待ってください。");tips.SetToolTip(address,"playit.ggで発行されたMinecraft Javaの接続先。自分の接続ポートはサーバー設定を確認してください。");
        memory.AccessibleName="サーバーの手動メモリ上限 GiB";address.AccessibleName="友達向けの公開接続先";command.AccessibleName="サーバーコンソールのコマンド入力";player.AccessibleName="参加を許可するJava版ユーザー名";log.AccessibleName="サーバーとplayit.ggのコンソールログ";
        connect.AccessibleName="playit.ggの接続を開始";disconnect.AccessibleName="playit.ggの接続を停止";
        usersGrid.AccessibleName="参加者と権限一覧";modsGrid.AccessibleName="サーバーMOD一覧";backupsGrid.AccessibleName="復元可能な完全バックアップ一覧";motdSetting.AccessibleName="サーバー紹介文";difficultySetting.AccessibleName="ゲームの難易度";modeSetting.AccessibleName="標準のゲームモード";maxPlayersSetting.AccessibleName="最大参加人数";viewSetting.AccessibleName="描画距離 チャンク数";simulationSetting.AccessibleName="シミュレーション距離 チャンク数";backupHours.AccessibleName="ワールド自動バックアップの時間間隔";backupKeep.AccessibleName="自動バックアップの保持数";restartTime.AccessibleName="毎日のサーバー再起動時刻";broadcastText.AccessibleName="全参加者へのお知らせ文";searchText.AccessibleName="コンソールログの検索語";
        CaptureStyle(this);workspace.Resize+=delegate{ResizeProfileInputs();};ResizeProfileInputs();RefreshGuide();RefreshExperience();
        oldHeading.Dispose();oldTools.Dispose();old.Dispose();
    }
    void StyleControls(Control rootControl)
    {
        foreach(Control c in rootControl.Controls){if(c is Button){var b=(Button)c;b.AccessibleName=b.Text;b.AccessibleDescription=b.Text+"を実行します。";b.MinimumSize=new Size(90,40);if(b is ActionButton){var a=(ActionButton)b;if(a.IconKey.Length==0)a.IconKey=IconFor(b.Text);if(a.Navigation){a.MinimumSize=new Size(176,36);a.Height=36;}}b.Font=new Font("Yu Gothic UI",10);}
            if(c is TextBox){c.AccessibleRole=AccessibleRole.Text;c.AccessibleName=c.AccessibleName??"入力欄";}
            if(c is DataGridView){var grid=(DataGridView)c;grid.RowTemplate.Height=32;grid.ColumnHeadersHeight=36;grid.GridColor=Color.FromArgb(55,68,84);grid.AccessibleName="一覧表";}
            StyleControls(c);}
    }
    string IconFor(string s){if(s.Contains("停止")||s.Contains("BAN"))return "stop";if(s.Contains("起動"))return "play";if(s.Contains("再起動")||s.Contains("更新")||s.Contains("取得"))return "refresh";if(s.Contains("コピー"))return "copy";if(s.Contains("バックアップ")||s.Contains("復元")||s.Contains("保存"))return "backup";if(s.Contains("設定"))return "settings";if(s.Contains("許可")||s.Contains("OP")||s.Contains("キック"))return "users";if(s.Contains("案内")||s.Contains("導入"))return "guide";if(s.Contains("接続")||s.Contains("連携"))return "link";if(s.Contains("フォルダー")||s.Contains("開く"))return "folder";return "console";}
    void CaptureStyle(Control c){baseFonts[c]=c.Font;baseColors[c]=new[]{c.BackColor,c.ForeColor};baseWidths[c]=c.Width;foreach(Control child in c.Controls)CaptureStyle(child);}
    void ResizeProfileInputs(){if(workspace==null)return;int available=Math.Max(250,workspace.ClientSize.Width-280);foreach(Control c in Descendants(workspace.TabPages[3])){int width;if(c is TextBox&&baseWidths.TryGetValue(c,out width))c.Width=Math.Min(width,available);if(c is Label&&c.MaximumSize.Width>0)c.MaximumSize=new Size(Math.Max(250,workspace.ClientSize.Width-48),0);}}
    void ApplyAccessibleTheme()
    {
        if(largeText==null||highContrast==null)return;float factor=largeText.Checked?1.16f:1f;foreach(var pair in baseFonts){Control c=pair.Key;if(c.IsDisposed)continue;Font f=pair.Value;c.Font=new Font(f.FontFamily,f.Size*factor,f.Style);Color[] colors=baseColors[c];c.BackColor=highContrast.Checked?Color.Black:colors[0];c.ForeColor=highContrast.Checked?Color.White:colors[1];}
        if(history!=null){history.BackColor=highContrast.Checked?Color.Black:Card;foreach(var a in history.ChartAreas){a.BackColor=history.BackColor;a.AxisX.LabelStyle.ForeColor=a.AxisY.LabelStyle.ForeColor=a.AxisY2.LabelStyle.ForeColor=highContrast.Checked?Color.White:Muted;}history.Invalidate();}foreach(var n in navigation)n.Invalidate();
    }
    void RefreshExperience()
    {
        if(autoMemory==null)return;bool running=Running(server);autoMemory.Enabled=!running&&!backupBusy;memory.Enabled=!running&&!autoMemory.Checked;
        if((DateTime.UtcNow-memoryPreviewAt).TotalSeconds>=10){memoryPreviewAt=DateTime.UtcNow;var info=PhysicalMemory();int budget=CurrentSharedBudget(false);string summary=running?"割当上限 "+activeHeapGiB+" GiB / 使用量は必要に応じて増減":autoMemory.Checked?"自動：登録 "+RegisteredCount()+"台 / 次回上限 約 "+budget+" GiB":"手動設定：上限 "+memory.Value+" GiB";heapPreview.Text=summary;heapExplanation.Text="搭載 "+info[0].ToString("0.0")+" GiB / 現在の空き "+info[1].ToString("0.0")+" GiB / 自動上限の目安 "+budget+" GiB\n登録数で予算を分け、OSと起動中の他サーバーの予約分を残します。登録追加後の再配分は各サーバーの次回起動時です。";}
        if(sideStatus!=null)sideStatus.Text=(running?(ready?"SERVER ONLINE":"SERVER STARTING"):"SERVER OFFLINE")+"\n127.0.0.1 : "+ServerPort;
        var values=new[]{cpuValue,ramValue,tpsValue,msptValue,uptimeValue,onlineValue,diskValue};string[] labels={"CPU使用率","プロセスメモリ","TPS","MSPT","稼働時間","オンライン人数","ディスク空き"};for(int i=0;i<values.Length;i++)if(values[i]!=null){values[i].AccessibleName=labels[i]+" "+values[i].Text;if(highContrast.Checked)values[i].ForeColor=Color.White;}
    }
    string GuideText(){return BuildParticipantGuide();}
    void AddParticipantPage()
    {
        var page=Page(workspace,"参加者向け案内");var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,54));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,56));page.Controls.Add(layout);
        layout.Controls.Add(new Label{Text="Discordにそのまま貼れる案内です。接続先欄の内容を反映して作成します。\n公開アドレスが未設定の間は、差し替え位置が表示されます。",ForeColor=Muted,Dock=DockStyle.Fill},0,0);
        participantGuide=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Card,ForeColor=Ink,Font=new Font("Yu Gothic UI",11),AccessibleName="Discord共有用の参加案内",AccessibleDescription="全選択してコピーできます。下のDiscord用コピーでもコピーできます。"};layout.Controls.Add(participantGuide,0,1);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(0,8,0,0)};var copy=FlowBtn(actions,"Discord用コピー",delegate{RefreshGuide();Clipboard.SetText(participantGuide.Text);Add("[案内] Discord向けの参加案内をコピーしました。");});copy.Width=180;FlowBtn(actions,"TXTで保存",delegate{RefreshGuide();File.WriteAllText(Path.Combine(root,"Discord参加案内.txt"),participantGuide.Text,new UTF8Encoding(true));Add("[案内] Discord参加案内.txtを保存しました。");});FlowBtn(actions,"接続先を反映",delegate{RefreshGuide();});layout.Controls.Add(actions,0,2);
    }
    void RefreshGuide(){if(participantGuide==null)return;participantGuide.Text=GuideText();File.WriteAllText(Path.Combine(root,"Discord参加案内.txt"),participantGuide.Text,new UTF8Encoding(true));}
    static void CaptureGenericCheck(AstralManager manager,string name,int page)
    {
        manager.workspace.SelectedIndex=page;Application.DoEvents();using(var image=new Bitmap(manager.Width,manager.Height)){manager.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(manager.root,name),System.Drawing.Imaging.ImageFormat.Png);}
    }
    static int CheckExperience()
    {
        string root=WorkspaceRoot;try{if(AutoHeap(64,60)!=57||AutoHeap(64,30)!=27||AutoHeap(8,3)>=4)throw new Exception("Automatic memory policy failed");
            var manager=new AstralManager();manager.CreateControl();manager.LoadSettings();manager.LoadFeatureSettings();if(manager.workspace.TabPages.Count!=9||manager.navigation.Count!=9)throw new Exception("Navigation missing");if(!manager.LaunchTarget().StartsWith(manager.profile.LaunchMode=="argfile"?"@":"-jar "))throw new Exception("Launch profile invalid");if(!manager.FtbBackupsAvailable&&manager.backupHours.Enabled)throw new Exception("Unavailable FTB controls must be disabled");if(manager.profile.ClientMode=="vanilla"&&!manager.GuideText().Contains("MODを入れる必要はありません"))throw new Exception("Vanilla guide incorrect");if(manager.profile.ClientMode=="modpack"&&!manager.GuideText().Contains(manager.profile.PackName))throw new Exception("Modpack guide incorrect");if(manager.GuideText().Length>2000||!manager.GuideText().Contains(manager.profile.Name))throw new Exception("Discord guide invalid");manager.largeText.Checked=true;manager.highContrast.Checked=true;manager.ApplyAccessibleTheme();if(manager.start.ForeColor!=Color.White)throw new Exception("Contrast mode not applied");manager.largeText.Checked=false;manager.highContrast.Checked=false;manager.ApplyAccessibleTheme();manager.Text="UI validation — synthetic fixture, no game server";manager.Show();Application.DoEvents();using(var image=new Bitmap(manager.Width,manager.Height)){manager.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(root,"ui-preview.png"),System.Drawing.Imaging.ImageFormat.Png);}CaptureGenericCheck(manager,"ui-settings.png",3);CaptureGenericCheck(manager,"ui-guide.png",7);CaptureGenericCheck(manager,"ui-discord.png",8);manager.workspace.SelectedIndex=0;manager.largeText.Checked=true;manager.highContrast.Checked=true;manager.ApplyAccessibleTheme();CaptureGenericCheck(manager,"ui-accessibility.png",0);manager.OpenServersDialog(true);manager.timer.Stop();manager.Dispose();File.WriteAllText(Path.Combine(root,"experience-verification.txt"),"PASS: 64GiB/60GiB free -> 57GiB heap; 64/30 -> 27GiB; low memory rejected. Nine pages, participant guide <=2000 characters, large text and high contrast initialized. Screenshot uses a synthetic fixture; no server or Discord connection.");return 0;
        }catch(Exception e){File.WriteAllText(Path.Combine(root,"experience-verification.txt"),"FAIL: "+e);return 1;}
    }
}
