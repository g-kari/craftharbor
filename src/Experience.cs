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
    public bool Selected,Navigation,Primary;
    bool hover;
    public ActionButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Cursor=Cursors.Hand;}
    public override Size GetPreferredSize(Size proposedSize){var text=TextRenderer.MeasureText(Text,Font);return new Size(Math.Max(MinimumSize.Width,text.Width+(IconKey.Length>0?(Navigation?72:62):28)),Math.Max(Navigation?50:44,text.Height+16));}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        bool contrast=HarborTheme.Contrast(this);
        Color bg=contrast?Color.Black:Selected||Primary?HarborTheme.Primary:BackColor;
        Color fg=contrast?(Enabled?Color.White:SystemColors.GrayText):Enabled?ForeColor:Color.FromArgb(155,171,179);
        if(contrast&&Selected){bg=Color.White;fg=Color.Black;}
        else if(hover&&Enabled)bg=HarborTheme.Lift(bg,6);
        e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);
        using(var path=SurfacePaint.Round(new RectangleF(1,1,Width-3,Height-3),3))
        using(var fill=new LinearGradientBrush(new Rectangle(0,0,Math.Max(1,Width),Math.Max(1,Height)),contrast?bg:HarborTheme.Lift(bg,7),bg,90f))
        using(var border=new Pen(contrast?Color.White:Selected||Primary?HarborTheme.Live:HarborTheme.Border,Selected||Primary?2:1)){e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(border,path);}
        if(!contrast&&Enabled)using(var bevel=new Pen(Color.FromArgb(48,HarborTheme.Text)))e.Graphics.DrawLine(bevel,5,3,Width-6,3);
        if(Selected)using(var marker=new SolidBrush(contrast?Color.Black:HarborTheme.Header))e.Graphics.FillRectangle(marker,3,7,4,Math.Max(1,Height-14));
        int icon=Navigation?30:22,inset=IconKey.Length>0?(Navigation?58:44):14;
        if(IconKey.Length>0){if(contrast||!Enabled)Icons.DrawMono(e.Graphics,IconKey,new Rectangle(14,(Height-icon)/2,icon,icon),fg);else Icons.Draw(e.Graphics,IconKey,new Rectangle(14,(Height-icon)/2,icon,icon),fg);}
        TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(inset,0,Math.Max(0,Width-inset-8),Height),fg,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        if(Focused){using(var p=new Pen(contrast?fg:HarborTheme.Focus,2)){p.DashStyle=DashStyle.Dot;e.Graphics.DrawRectangle(p,5,5,Math.Max(0,Width-11),Math.Max(0,Height-11));}}
    }
}

static class Icons
{
    static readonly string[] Keys={"server","public","chart","console","users","settings","backup","clock","mods","guide","discord","cpu","ram","tps","mspt","disk","copy","save","link","folder","play","stop","refresh","lighthouse"};
    static readonly Rectangle[] Regions={new Rectangle(35,65,197,207),new Rectangle(281,90,215,165),new Rectangle(537,65,196,195),new Rectangle(804,79,192,175),new Rectangle(1048,100,217,140),new Rectangle(1311,67,195,196),new Rectangle(37,320,191,191),new Rectangle(294,315,184,187),new Rectangle(528,315,210,195),new Rectangle(823,315,154,187),new Rectangle(1051,339,209,154),new Rectangle(1310,308,199,200),new Rectangle(30,555,218,187),new Rectangle(272,569,227,173),new Rectangle(537,548,201,204),new Rectangle(812,555,175,193),new Rectangle(1081,558,166,184),new Rectangle(1320,559,179,177),new Rectangle(35,793,192,184),new Rectangle(298,804,191,159),new Rectangle(581,800,139,171),new Rectangle(823,810,154,154),new Rectangle(1071,799,176,177),new Rectangle(1296,770,221,221)};
    public static void Draw(Graphics g,string key,Rectangle r,Color c)
    {
        int index=Array.IndexOf(Keys,key);Image atlas=HarborTheme.IconAtlas;
        if(index<0||atlas==null||c==Color.White||c==Color.Yellow){DrawMono(g,key,r,c);return;}
        Rectangle source=Regions[index];float ratio=Math.Min(r.Width/(float)source.Width,r.Height/(float)source.Height);
        int w=Math.Max(1,(int)(source.Width*ratio)),h=Math.Max(1,(int)(source.Height*ratio));
        var saved=g.Save();g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.Half;
        g.DrawImage(atlas,new Rectangle(r.X+(r.Width-w)/2,r.Y+(r.Height-h)/2,w,h),source,GraphicsUnit.Pixel);g.Restore(saved);
    }
    public static void DrawMono(Graphics g,string key,Rectangle r,Color c)
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

partial class CraftHarbor
{
    CheckBox autoMemory,largeText,highContrast;
    Label heapPreview,heapExplanation,pageHeading;
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
        Controls.Remove(old);
        heapPreview=new Label{ForeColor=Muted,AccessibleName="サーバーメモリの割り当て状況"};
        var serverForm=(FlowLayoutPanel)workspace.TabPages[3].Controls[0];
        var memoryGroup=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0,0,0,18)};memoryGroup.Controls.Add(new Label{Text="メモリ割り当て",AutoSize=true,Font=new Font("Yu Gothic UI",13,FontStyle.Bold)});autoMemory=new HarborCheckBox{Text="自動（専用PC向け・初期値）",AutoSize=true,Checked=true,AccessibleName="サーバーメモリを自動調整",AccessibleDescription="起動時の物理メモリと空き容量から上限を決めます。OSの動作分を残します。"};memoryGroup.Controls.Add(autoMemory);
        var manualRow=new FlowLayoutPanel{AutoSize=true,WrapContents=false};manualRow.Controls.Add(new Label{Text="手動上限",Width=110,Margin=new Padding(0,4,8,0)});var info=PhysicalMemory();memory.Maximum=Math.Max(4,(int)Math.Floor(info[0]-Math.Max(4,info[0]*.08)));memory.Width=90;memory.Location=Point.Empty;manualRow.Controls.Add(memory);manualRow.Controls.Add(new Label{Text="GiB",AutoSize=true,Margin=new Padding(8,4,0,0)});memoryGroup.Controls.Add(manualRow);
        heapExplanation=new Label{AutoSize=true,MaximumSize=new Size(760,0),ForeColor=Muted};memoryGroup.Controls.Add(heapExplanation);serverForm.Controls.Add(memoryGroup);serverForm.Controls.SetChildIndex(memoryGroup,0);

        InitializeJavaSettings(serverForm);BuildHarborShell(cards,join,allow);
        autoMemory.CheckedChanged+=delegate{memoryPreviewAt=DateTime.MinValue;RefreshExperience();};
        largeText.CheckedChanged+=delegate{ApplyAccessibleTheme();};highContrast.CheckedChanged+=delegate{ApplyAccessibleTheme();};
        KeyPreview=true;KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Alt&&e.KeyCode>=Keys.D1&&e.KeyCode<=Keys.D9){int n=e.KeyCode-Keys.D1;workspace.SelectedIndex=n;navigation[n].Focus();e.Handled=true;}if(e.Control&&e.KeyCode==Keys.L){workspace.SelectedIndex=1;command.Focus();e.SuppressKeyPress=true;}if(e.KeyCode==Keys.F1){workspace.SelectedIndex=7;e.Handled=true;}};
        AddProfileSettings();RefreshProfileLabels();StyleControls(this);
        tips.SetToolTip(start,"サーバーを起動します。公開接続は設定した方法で別に開始します。");
        tips.SetToolTip(stop,"ワールドの保存が完了してからサーバーを停止します。");
        tips.SetToolTip(address,"現在の公開方法で取得または保存した、友達に渡す接続先です。");
        memory.AccessibleName="サーバーの手動メモリ上限 GiB";address.AccessibleName="友達向けの公開接続先";command.AccessibleName="サーバーコンソールのコマンド入力";player.AccessibleName="参加を許可するJava版ユーザー名";log.AccessibleName="Minecraftと公開接続のコンソールログ";
        usersGrid.AccessibleName="参加者と権限一覧";modsGrid.AccessibleName="サーバーMOD一覧";backupsGrid.AccessibleName="復元可能な完全バックアップ一覧";motdSetting.AccessibleName="サーバー紹介文";difficultySetting.AccessibleName="ゲームの難易度";modeSetting.AccessibleName="標準のゲームモード";maxPlayersSetting.AccessibleName="最大参加人数";viewSetting.AccessibleName="描画距離 チャンク数";simulationSetting.AccessibleName="シミュレーション距離 チャンク数";backupHours.AccessibleName="ワールド自動バックアップの時間間隔";backupKeep.AccessibleName="自動バックアップの保持数";restartTime.AccessibleName="毎日のサーバー再起動時刻";broadcastText.AccessibleName="全参加者へのお知らせ文";searchText.AccessibleName="コンソールログの検索語";

        ObserveKeyboardReview(this);CaptureStyle(this);ResizeProfileInputs();RefreshGuide();RefreshExperience();RefreshRecentLogs();RefreshDashboardPresentation(false,false);ArrangeHarbor();
        if(highContrast.Checked)ApplyAccessibleTheme();
        oldHeading.Dispose();oldTools.Dispose();old.Dispose();
    }
    void StyleControls(Control rootControl)
    {
        foreach(Control c in rootControl.Controls.Cast<Control>().ToArray()){if(c is Label)c.BackColor=Color.Transparent;if(c.Font.SizeInPoints<12)c.Font=new Font(c.Font.FontFamily,12,c.Font.Style);if(c is Button){var b=(Button)c;b.AccessibleName=b.Text;b.AccessibleDescription=b.Text+"を実行します。";b.MinimumSize=new Size(Math.Max(90,b.MinimumSize.Width),44);if(b is ActionButton){var a=(ActionButton)b;if(a.IconKey.Length==0)a.IconKey=IconFor(b.Text);if(a.Navigation){a.MinimumSize=new Size(180,50);a.Height=50;}else if(b.Parent is FlowLayoutPanel)b.AutoSize=true;}b.AutoSizeMode=AutoSizeMode.GrowAndShrink;b.Font=new Font("Yu Gothic UI",b is ActionButton&&((ActionButton)b).Navigation?13.5f:12.5f,b is ActionButton&&(((ActionButton)b).Navigation||((ActionButton)b).Primary)?FontStyle.Bold:FontStyle.Regular);}
            if(c is TextBox){c.AccessibleRole=AccessibleRole.Text;c.AccessibleName=c.AccessibleName??"入力欄";}
            if(c is DataGridView){var grid=(DataGridView)c;grid.RowTemplate.Height=36;grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;grid.DefaultCellStyle.Padding=new Padding(8,4,8,4);grid.ColumnHeadersDefaultCellStyle.Padding=new Padding(8,4,8,4);grid.ColumnHeadersHeight=Math.Max(36,grid.Font.Height+12);grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.Single;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;grid.GridColor=HarborTheme.Border;if(string.IsNullOrWhiteSpace(grid.AccessibleName))grid.AccessibleName="一覧表";}
            if(c is TextBox||c is NumericUpDown||c is ComboBox)InputFrame.Wrap(c);else StyleControls(c);}
    }
    string IconFor(string s){if(s.Contains("停止")||s.Contains("BAN"))return "stop";if(s.Contains("起動"))return "play";if(s.Contains("再起動")||s.Contains("更新")||s.Contains("取得"))return "refresh";if(s.Contains("コピー"))return "copy";if(s.Contains("保存"))return "save";if(s.Contains("バックアップ")||s.Contains("復元"))return "backup";if(s.Contains("設定"))return "settings";if(s.Contains("許可")||s.Contains("OP")||s.Contains("キック"))return "users";if(s.Contains("案内")||s.Contains("導入"))return "guide";if(s.Contains("接続")||s.Contains("連携"))return "link";if(s.Contains("フォルダー")||s.Contains("開く"))return "folder";return "console";}
    void CaptureStyle(Control c){baseFonts[c]=c.Font;baseColors[c]=new[]{c.BackColor,c.ForeColor};baseWidths[c]=c.Width;foreach(Control child in c.Controls)CaptureStyle(child);}
    void ResizeProfileInputs(){if(workspace==null)return;int available=Math.Max(230,workspace.ClientSize.Width-280);foreach(Control c in Descendants(workspace.TabPages[3])){int width;if(c is TextBox&&baseWidths.TryGetValue(c,out width)){if(c.Parent is InputFrame)c.Parent.Width=Math.Min(width+24,available);else c.Width=Math.Min(width,available);}if(c is Label&&c.MaximumSize.Width>0)c.MaximumSize=new Size(Math.Max(250,workspace.ClientSize.Width-48),0);}}
    void ApplyAccessibleTheme()
    {
        if(largeText==null||highContrast==null)return;float factor=largeText.Checked?1.16f:1f;foreach(var pair in baseFonts){Control c=pair.Key;if(c.IsDisposed)continue;Font f=pair.Value;c.Font=new Font(f.FontFamily,f.Size*factor,f.Style);Color[] colors=baseColors[c];c.BackColor=highContrast.Checked?Color.Black:colors[0];c.ForeColor=highContrast.Checked?Color.White:colors[1];}
        if(history!=null){history.BackColor=highContrast.Checked?Color.Black:Card;foreach(var a in history.ChartAreas){a.BackColor=history.BackColor;a.AxisX.LabelStyle.ForeColor=a.AxisY.LabelStyle.ForeColor=a.AxisY2.LabelStyle.ForeColor=highContrast.Checked?Color.White:Muted;}foreach(var title in history.Titles)title.ForeColor=highContrast.Checked?Color.White:Muted;foreach(var legend in history.Legends)legend.ForeColor=highContrast.Checked?Color.White:Ink;history.Invalidate();}foreach(var n in navigation)n.Invalidate();ApplyHarborContrast();ArrangeHarbor();Invalidate(true);
    }
    void ThemeDialog(Form dialog)
    {
        bool contrast=highContrast!=null&&highContrast.Checked;float factor=largeText!=null&&largeText.Checked?1.16f:1;
        var controls=new[]{(Control)dialog}.Concat(Descendants(dialog)).ToArray();var fonts=controls.ToDictionary(c=>c,c=>c.Font);
        foreach(var c in controls){var font=fonts[c];c.Font=new Font(font.FontFamily,Math.Max(12,font.SizeInPoints)*factor,font.Style);if(contrast){c.BackColor=Color.Black;c.ForeColor=Color.White;var link=c as LinkLabel;if(link!=null)link.LinkColor=link.ActiveLinkColor=link.VisitedLinkColor=Color.White;var grid=c as DataGridView;if(grid!=null){grid.BackgroundColor=Color.Black;grid.DefaultCellStyle.BackColor=grid.ColumnHeadersDefaultCellStyle.BackColor=Color.Black;grid.DefaultCellStyle.ForeColor=grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;grid.DefaultCellStyle.SelectionBackColor=Color.White;grid.DefaultCellStyle.SelectionForeColor=Color.Black;}}}
        Action fit=delegate{FitContentWidth(dialog,Math.Max(200,dialog.ClientSize.Width-48));FitFlowRows(dialog);};dialog.Shown+=delegate{fit();};dialog.Resize+=delegate{fit();};fit();ObserveKeyboardReview(dialog);var area=Screen.FromControl(this).WorkingArea;dialog.Size=new Size(Math.Min(dialog.Width,area.Width),Math.Min(dialog.Height,area.Height));
    }
    void RefreshExperience()
    {
        if(autoMemory==null)return;bool running=Running(server);autoMemory.Enabled=!running&&!backupBusy;memory.Enabled=!running&&!autoMemory.Checked;
        if((DateTime.UtcNow-memoryPreviewAt).TotalSeconds>=10){memoryPreviewAt=DateTime.UtcNow;var info=PhysicalMemory();int budget=CurrentSharedBudget(false);string summary=running?"割当上限 "+activeHeapGiB+" GiB / 使用量は必要に応じて増減":autoMemory.Checked?"自動：登録 "+RegisteredCount()+"台 / 次回上限 約 "+budget+" GiB":"手動設定：上限 "+memory.Value+" GiB";heapPreview.Text=summary;heapExplanation.Text="搭載 "+info[0].ToString("0.0")+" GiB / 現在の空き "+info[1].ToString("0.0")+" GiB / 自動上限の目安 "+budget+" GiB\n登録数で予算を分け、OSと起動中の他サーバーの予約分を残します。登録追加後の再配分は各サーバーの次回起動時です。";}
        if((DateTime.UtcNow-distancePreviewAt).TotalSeconds>=10){RefreshDistancePreview();RefreshJavaPreview();}
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
    static void CaptureGenericCheck(CraftHarbor manager,string name,int page)
    {
        manager.workspace.SelectedIndex=page;Application.DoEvents();using(var image=new Bitmap(manager.Width,manager.Height)){manager.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(manager.root,name),System.Drawing.Imaging.ImageFormat.Png);}
    }
    static int CheckExperience()
    {
        string root=WorkspaceRoot;try{Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);if(AutoHeap(64,60)!=57||AutoHeap(64,30)!=27||AutoHeap(8,3)>=4)throw new Exception("Automatic memory policy failed");
            var manager=new CraftHarbor();manager.CreateControl();manager.timer.Stop();manager.LoadSettings();manager.LoadFeatureSettings();manager.CheckDistanceWorkflow();manager.CheckMetricChart();if(manager.workspace.TabPages.Count!=9||manager.navigation.Count!=9)throw new Exception("Navigation missing");if(!manager.LaunchTarget().StartsWith(manager.profile.LaunchMode=="argfile"?"@":"-jar "))throw new Exception("Launch profile invalid");if(!manager.FtbBackupsAvailable&&manager.backupHours.Enabled)throw new Exception("Unavailable FTB controls must be disabled");if(manager.profile.ClientMode=="vanilla"&&!manager.GuideText().Contains("MODを入れる必要はありません"))throw new Exception("Vanilla guide incorrect");if(manager.profile.ClientMode=="modpack"&&!manager.GuideText().Contains(manager.profile.PackName))throw new Exception("Modpack guide incorrect");if(manager.GuideText().Length>2000||!manager.GuideText().Contains(manager.profile.Name))throw new Exception("Discord guide invalid");manager.largeText.Checked=true;manager.highContrast.Checked=true;manager.ApplyAccessibleTheme();if(manager.start.ForeColor!=Color.White)throw new Exception("Contrast mode not applied");manager.largeText.Checked=false;manager.highContrast.Checked=false;manager.ApplyAccessibleTheme();manager.Text="UI validation — synthetic fixture, no game server";manager.Show();Application.DoEvents();using(var image=new Bitmap(manager.Width,manager.Height)){manager.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(root,"ui-preview.png"),System.Drawing.Imaging.ImageFormat.Png);}manager.AppendLogRecords(new[]{"[MC] Synthetic Minecraft log sample","[公開] Synthetic tunnel log sample","[管理] Synthetic manager log sample"});manager.logSourceFilter.SelectedIndex=1;if(!manager.log.Text.Contains("Minecraft log")||manager.log.Text.Contains("tunnel log"))throw new Exception("Game log filter incorrect");manager.logSourceFilter.SelectedIndex=2;if(!manager.log.Text.Contains("tunnel log")||manager.log.Text.Contains("Minecraft log"))throw new Exception("Public log filter incorrect");manager.logSourceFilter.SelectedIndex=3;if(!manager.log.Text.Contains("manager log")||manager.log.Text.Contains("tunnel log"))throw new Exception("Manager log filter incorrect");manager.logSourceFilter.SelectedIndex=0;CaptureGenericCheck(manager,"ui-console.png",1);CaptureGenericCheck(manager,"ui-settings.png",3);CaptureGenericCheck(manager,"ui-guide.png",7);CaptureGenericCheck(manager,"ui-discord.png",8);manager.workspace.SelectedIndex=0;manager.largeText.Checked=true;manager.highContrast.Checked=true;manager.ApplyAccessibleTheme();CaptureGenericCheck(manager,"ui-accessibility.png",0);File.AppendAllText(Path.Combine(root,"ui-check-progress.txt"),"Existing UI checks\n");manager.CheckReviewScreens();File.AppendAllText(Path.Combine(root,"ui-check-progress.txt"),"Harbor UI checks\n");manager.CheckHarborScreens();manager.OpenServersDialog(true);manager.HostingSettings(true);ShowPackImport(manager,root,"",true);manager.timer.Stop();manager.Dispose();File.WriteAllText(Path.Combine(root,"experience-verification.txt"),"PASS: 64GiB/60GiB free -> 57GiB heap; 64/30 -> 27GiB; low memory rejected. Nine pages, participant guide <=2000 characters, large text and high contrast initialized. Screenshot uses a synthetic fixture; no server or Discord connection.");return 0;
        }catch(Exception e){File.WriteAllText(Path.Combine(root,"experience-verification.txt"),"FAIL: "+e);return 1;}
    }
}
