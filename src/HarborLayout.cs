using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

partial class CraftHarbor
{
    TableLayoutPanel harborMain,harborHeader,dashboardRows,historyAndLogs,sidebarLayout,harborBody,harborShell,addressRow;
    FlowLayoutPanel headerActions;
    Label dashboardTitle,footerState;
    TextBox recentLogs;
    HarborArtwork sidebarArt;
    ActionButton launchAll;
    Button shareAccountButton;
    bool arrangingHarbor;
    static string[] PageNames={"ダッシュボード","コンソール","参加者・権限","サーバー設定","バックアップ","運用・予約","MOD・プラグイン","参加者向け案内","Discord通知"};
    Label HarborLabel(string text,float size,bool bold){return new Label{Text=text,AutoSize=true,Font=new Font("Yu Gothic UI",size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=Ink,Margin=Padding.Empty,Tag="harbor-text"};}
    void BuildHarborShell(Control oldCards,Control oldJoin,Control allow)
    {
        var publicSettings=oldCards.Controls[1].Controls.OfType<Button>().First(b=>b!=connect&&b!=disconnect);
        var joinActions=oldJoin.Controls.OfType<TableLayoutPanel>().First().Controls.OfType<Button>().ToArray();
        shareAccountButton=joinActions[2];
        var oldDashboard=workspace.TabPages[0].Controls[0];
        var metricActions=oldDashboard.Controls.OfType<FlowLayoutPanel>().First().Controls.OfType<Button>().ToArray();
        AddParticipantPage();AddDiscordPage();

        var shell=harborShell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,BackColor=Bg,Padding=new Padding(4),Margin=Padding.Empty};
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute,60));shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,34));Controls.Add(shell);
        shell.Paint+=delegate(object sender,PaintEventArgs e){HarborTheme.Frame(e.Graphics,shell.ClientRectangle,true,highContrast!=null&&highContrast.Checked);};
        var masthead=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=HarborTheme.Header,Margin=Padding.Empty,Padding=new Padding(14,3,14,3)};
        masthead.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));masthead.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        masthead.Controls.Add(new HarborArtwork{LogoOnly=true,Dock=DockStyle.Fill,BackColor=HarborTheme.Header},0,0);
        masthead.Controls.Add(new Label{Text="CraftHarbor",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",24,FontStyle.Bold),ForeColor=Color.FromArgb(40,29,19),BackColor=HarborTheme.Header},1,0);shell.Controls.Add(masthead,0,0);
        var body=harborBody=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,256));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));shell.Controls.Add(body,0,1);
        sidebarLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,BackColor=HarborTheme.Input,Padding=new Padding(14,18,14,12),Margin=Padding.Empty};
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,116));sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,100));sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,144));body.Controls.Add(sidebarLayout,0,0);
        var identity=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        serverIdentity=HarborLabel(profile.Name,12,true);serverIdentity.MaximumSize=new Size(216,54);serverIdentity.AutoEllipsis=true;identity.Controls.Add(serverIdentity);
        var choose=(ActionButton)FlowBtn(identity,"サーバー一覧",delegate{OpenServersDialog();});choose.IconKey="folder";choose.AccessibleDescription="サーバーを登録・切替、または別のウィンドウで開きます。";sidebarLayout.Controls.Add(identity,0,0);
        var nav=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Margin=Padding.Empty};
        string[] icons={"chart","console","users","settings","backup","clock","mods","guide","link"};
        for(int i=0;i<PageNames.Length;i++){int index=i;var button=(ActionButton)Btn(nav,PageNames[i],0,0,218,delegate{workspace.SelectedIndex=index;});button.Navigation=true;button.IconKey=icons[i];button.Height=46;button.Margin=new Padding(0,0,0,5);button.AccessibleName=PageNames[i];tips.SetToolTip(button,PageNames[i]+" / Alt+"+(i+1));navigation.Add(button);}
        nav.Resize+=delegate{foreach(var button in navigation)button.Width=Math.Max(180,nav.ClientSize.Width-20);};sidebarLayout.Controls.Add(nav,0,1);
        var access=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(0,12,0,0)};
        largeText=new HarborCheckBox{Text="文字を大きく",AutoSize=true,AccessibleName="文字を大きく表示"};
        highContrast=new HarborCheckBox{Text="高コントラスト",AutoSize=true,Checked=SystemInformation.HighContrast,AccessibleName="高コントラスト表示"};access.Controls.Add(largeText);access.Controls.Add(highContrast);sidebarLayout.Controls.Add(access,0,2);
        sidebarArt=new HarborArtwork{Dock=DockStyle.Fill,BackColor=HarborTheme.Input};sidebarLayout.Controls.Add(sidebarArt,0,3);

        harborMain=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(16,12,12,8),Margin=Padding.Empty};
        harborMain.RowStyles.Add(new RowStyle(SizeType.Absolute,112));harborMain.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.Controls.Add(harborMain,1,0);
        harborHeader=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty};
        harborHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));harborHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,372));
        harborHeader.RowStyles.Add(new RowStyle(SizeType.Percent,100));harborHeader.RowStyles.Add(new RowStyle(SizeType.Absolute,0));
        var titles=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute,26));titles.RowStyles.Add(new RowStyle(SizeType.Absolute,48));titles.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        pageHeading=HarborLabel(PageNames[0],12,false);pageHeading.ForeColor=Muted;pageHeading.Dock=DockStyle.Fill;titles.Controls.Add(pageHeading,0,0);
        dashboardTitle=HarborLabel(profile.Name,22,true);dashboardTitle.AutoSize=false;dashboardTitle.AutoEllipsis=true;dashboardTitle.Dock=DockStyle.Fill;titles.Controls.Add(dashboardTitle,0,1);
        profileSummary=HarborLabel(ProfileMetadata(),12,false);profileSummary.AutoSize=false;profileSummary.AutoEllipsis=true;profileSummary.Dock=DockStyle.Fill;profileSummary.ForeColor=Muted;titles.Controls.Add(profileSummary,0,2);
        harborHeader.Controls.Add(titles,0,0);
        headerActions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Padding=new Padding(4,8,0,0)};
        var help=(ActionButton)FlowBtn(headerActions,"参加者の案内",delegate{workspace.SelectedIndex=7;});help.IconKey="guide";
        launchAll=(ActionButton)FlowBtn(headerActions,"一括起動",delegate{StartServer();StartTunnel();});launchAll.Primary=true;launchAll.IconKey="play";
        harborHeader.Controls.Add(headerActions,1,0);harborMain.Controls.Add(harborHeader,0,0);workspace.Dock=DockStyle.Fill;harborMain.Controls.Add(workspace,0,1);

        var dashboard=workspace.TabPages[0];dashboard.Controls.Remove(oldDashboard);dashboard.Padding=Padding.Empty;dashboard.AutoScroll=true;
        dashboardRows=new TableLayoutPanel{Dock=DockStyle.Top,ColumnCount=1,RowCount=4,Height=860,Margin=Padding.Empty};
        foreach(int h in new[]{180,104,196,376})dashboardRows.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
        dashboard.Controls.Add(dashboardRows);
        var status=new HarborFrame{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=Card,Margin=new Padding(0,0,4,10)};
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        status.Controls.Add(StatusColumn(serverState,new[]{start,stop},heapPreview),0,0);
        hostingSummary=new Label{Dock=DockStyle.Fill,ForeColor=Muted,Margin=Padding.Empty,AutoEllipsis=true,Text=HostingName+" / 接続先を確認中"};
        connect.Text="開始";disconnect.Text="停止";publicSettings.Text="設定";
        status.Controls.Add(StatusColumn(tunnelState,new[]{connect,disconnect,publicSettings},hostingSummary),1,0);dashboardRows.Controls.Add(status,0,0);((ActionButton)start).Primary=true;
        var join=new SurfaceTable{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,BackColor=Card,Padding=new Padding(12,8,12,8),Margin=new Padding(0,0,4,10)};
        join.RowStyles.Add(new RowStyle(SizeType.Absolute,28));join.RowStyles.Add(new RowStyle(SizeType.Percent,100));join.Controls.Add(HarborLabel("友達に渡す接続先",12,true),0,0);
        addressRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty};
        addressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));foreach(int width in new[]{112,100,188})addressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,width));
        address.Dock=DockStyle.Fill;address.Margin=new Padding(0,0,8,0);addressRow.Controls.Add(address,0,0);
        for(int i=0;i<joinActions.Length;i++){joinActions[i].Dock=DockStyle.Fill;joinActions[i].Margin=new Padding(0,0,i==2?0:6,0);addressRow.Controls.Add(joinActions[i],i+1,0);}join.Controls.Add(addressRow,0,1);dashboardRows.Controls.Add(join,0,1);

        var metrics=new HarborFrame{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,BackColor=Card,Margin=new Padding(0,0,4,10),Padding=new Padding(12,8,12,8)};
        metrics.RowStyles.Add(new RowStyle(SizeType.Absolute,28));metrics.RowStyles.Add(new RowStyle(SizeType.Percent,60));metrics.RowStyles.Add(new RowStyle(SizeType.Percent,40));metrics.Controls.Add(HarborLabel("サーバーの状態",14,true),0,0);
        var major=MetricBand(new[]{cpuValue,ramValue,tpsValue,msptValue},new[]{"CPU / PC全体比","RAM / プロセス","TPS / 直近5秒","MSPT / 中央値"},false);
        var minor=MetricBand(new[]{uptimeValue,onlineValue,diskValue},new[]{"稼働時間","オンライン","ディスク空き"},true);
        metrics.Controls.Add(major,0,1);metrics.Controls.Add(minor,0,2);dashboardRows.Controls.Add(metrics,0,2);
        historyAndLogs=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty};
        historyAndLogs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,64));historyAndLogs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,36));
        historyAndLogs.RowStyles.Add(new RowStyle(SizeType.Percent,100));historyAndLogs.RowStyles.Add(new RowStyle(SizeType.Absolute,0));
        var graph=new HarborFrame{Dock=DockStyle.Fill,BackColor=Card,RowCount=4,ColumnCount=1,Padding=new Padding(10),Margin=new Padding(0,0,8,0)};
        graph.RowStyles.Add(new RowStyle(SizeType.Absolute,32));graph.RowStyles.Add(new RowStyle(SizeType.Percent,100));graph.RowStyles.Add(new RowStyle(SizeType.Absolute,52));graph.RowStyles.Add(new RowStyle(SizeType.Absolute,56));graph.Controls.Add(HarborLabel("CPU・RAMの履歴",14,true),0,0);history.Dock=DockStyle.Fill;graph.Controls.Add(history,0,1);metricsNote.Dock=DockStyle.Fill;graph.Controls.Add(metricsNote,0,2);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true,Margin=Padding.Empty};foreach(var button in metricActions){button.Margin=new Padding(0,4,6,0);actions.Controls.Add(button);}graph.Controls.Add(actions,0,3);historyAndLogs.Controls.Add(graph,0,0);
        var logs=new HarborFrame{Dock=DockStyle.Fill,BackColor=Card,RowCount=3,ColumnCount=1,Padding=new Padding(10),Margin=new Padding(0,0,4,0)};
        logs.RowStyles.Add(new RowStyle(SizeType.Absolute,32));logs.RowStyles.Add(new RowStyle(SizeType.Percent,100));logs.RowStyles.Add(new RowStyle(SizeType.Absolute,56));logs.Controls.Add(HarborLabel("最近のログ",14,true),0,0);
        recentLogs=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,BackColor=HarborTheme.Input,ForeColor=Ink,Font=new Font("Yu Gothic UI",12),AccessibleName="最近のログ（すべての種類）"};logs.Controls.Add(recentLogs,0,1);
        var openConsole=(ActionButton)Btn(logs,"コンソールを開く",0,0,220,delegate{workspace.SelectedIndex=1;command.Focus();});openConsole.Dock=DockStyle.Fill;openConsole.IconKey="console";logs.Controls.Remove(openConsole);logs.Controls.Add(openConsole,0,2);
        historyAndLogs.Controls.Add(logs,1,0);dashboardRows.Controls.Add(historyAndLogs,0,3);
        oldDashboard.Dispose();

        var usersLayout=(TableLayoutPanel)workspace.TabPages[2].Controls[0];usersLayout.RowCount=4;usersLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,100));allow.Dock=DockStyle.Fill;((FlowLayoutPanel)allow).WrapContents=true;usersLayout.Controls.Add(allow,0,3);
        footerState=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Muted,Padding=new Padding(12,0,0,0),AutoEllipsis=true,AccessibleName="サーバーと公開接続の状態"};shell.Controls.Add(footerState,0,2);
        workspace.SelectedIndexChanged+=delegate{SelectHarborPage();};navigation[0].Selected=true;
        Resize+=delegate{ArrangeHarbor();};workspace.Resize+=delegate{ArrangeHarbor();};
    }
    Control StatusColumn(Label state,Button[] buttons,Label note)
    {
        var column=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(8,2,8,2),Margin=Padding.Empty};
        column.RowStyles.Add(new RowStyle(SizeType.Absolute,36));column.RowStyles.Add(new RowStyle(SizeType.Percent,100));column.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
        state.Margin=Padding.Empty;state.AutoSize=false;state.Dock=DockStyle.Fill;state.Font=new Font("Yu Gothic UI",14,FontStyle.Bold);state.AutoEllipsis=true;column.Controls.Add(state,0,0);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true,Margin=Padding.Empty};foreach(var button in buttons){button.AutoSize=true;button.Margin=new Padding(0,2,6,2);actions.Controls.Add(button);}column.Controls.Add(actions,0,1);
        note.Margin=Padding.Empty;note.AutoSize=false;note.Dock=DockStyle.Fill;note.AutoEllipsis=true;note.Font=new Font("Yu Gothic UI",12);column.Controls.Add(note,0,2);return column;
    }
    TableLayoutPanel MetricBand(Label[] values,string[] labels,bool small)
    {
        var band=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=values.Length,RowCount=1,Margin=Padding.Empty};
        for(int i=0;i<values.Length;i++){band.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/values.Length));var tile=new SurfaceTable{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(10,4,8,4),BackColor=Card,Margin=new Padding(0,0,4,4)};tile.RowStyles.Add(new RowStyle(SizeType.Absolute,small?26:46));tile.RowStyles.Add(new RowStyle(SizeType.Percent,100));var caption=HarborLabel(labels[i],12,false);caption.AutoSize=false;caption.Dock=DockStyle.Fill;caption.AutoEllipsis=false;caption.ForeColor=Muted;tile.Controls.Add(caption,0,0);tips.SetToolTip(caption,labels[i]);values[i].Margin=Padding.Empty;values[i].Font=new Font("Segoe UI",small?14:20,FontStyle.Bold);values[i].ForeColor=Ink;values[i].AutoEllipsis=true;values[i].Dock=DockStyle.Fill;tile.Controls.Add(values[i],0,1);band.Controls.Add(tile,i,0);}return band;
    }
    void SelectHarborPage()
    {
        if(pageHeading==null||workspace.SelectedIndex<0)return;pageHeading.Text=PageNames[workspace.SelectedIndex];dashboardTitle.Text=workspace.SelectedIndex==0?profile.Name:PageNames[workspace.SelectedIndex];
        tips.SetToolTip(dashboardTitle,profile.Name);for(int i=0;i<navigation.Count;i++){navigation[i].Selected=i==workspace.SelectedIndex;navigation[i].AccessibleDescription=(navigation[i].Selected?"現在の画面。":"画面へ移動。")+"Alt+"+(i+1);navigation[i].Invalidate();}if(workspace.SelectedIndex==7)RefreshGuide();ArrangeHarbor();
    }
    void RefreshRecentLogs()
    {
        if(recentLogs==null)return;string value=logRecords.Count==0?"まだログはありません。起動や接続の記録がここに表示されます。":string.Join(Environment.NewLine,logRecords.Skip(Math.Max(0,logRecords.Count-10)));
        if(recentLogs.Text==value)return;recentLogs.Text=value;recentLogs.SelectionStart=recentLogs.TextLength;recentLogs.ScrollToCaret();
    }
    void RefreshDashboardPresentation(bool sr,bool tr)
    {
        string state=preparingServer?"Javaを確認中…":sr?(stopping?"保存・停止中":ready?"稼働中":"起動中…"):"停止中";
        serverState.Text=(sr&&ready&&!stopping?"● ":"■ ")+"サーバー："+state;
        serverState.ForeColor=highContrast.Checked?Color.White:sr&&ready&&!stopping?HarborTheme.Live:Ink;
        tunnelState.Text=(tr?"● ":"■ ")+"公開接続："+(tr?"実行中":hosting.Provider=="direct"?"手動設定":"停止中");
        tunnelState.ForeColor=highContrast.Checked?Color.White:tr?HarborTheme.Live:Ink;
        start.Enabled=!sr&&!preparingServer&&!backupBusy&&!closing;stop.Enabled=sr&&!stopping;
        connect.Enabled=!tr&&!closing;disconnect.Enabled=tr;backup.Enabled=!sr&&!preparingServer&&!backupBusy&&!closing;memory.Enabled=!sr&&!preparingServer&&!autoMemory.Checked;
        launchAll.Enabled=start.Enabled||connect.Enabled;shareAccountButton.Text=hosting.Provider=="playit"||hosting.Provider=="ngrok"?"アカウント連携":"公開設定";shareAccountButton.AccessibleName=shareAccountButton.Text;
        connect.AccessibleName=HostingName+"の接続を開始";disconnect.AccessibleName=HostingName+"の接続を停止";
        footerState.Text=serverState.Text+"    |    "+tunnelState.Text+" / "+HostingName;
    }
    void OpenHostingAccount(){if(hosting.Provider=="playit")Open(claimUrl.Length>0?claimUrl:"https://playit.gg/account");else if(hosting.Provider=="ngrok")Open("https://dashboard.ngrok.com/");else HostingSettings();}
    void ApplyHarborContrast()
    {
        bool contrast=highContrast.Checked;foreach(var grid in Descendants(this).OfType<DataGridView>()){grid.BackgroundColor=contrast?Color.Black:Card;grid.DefaultCellStyle.BackColor=contrast?Color.Black:Bg;grid.DefaultCellStyle.ForeColor=contrast?Color.White:Ink;grid.DefaultCellStyle.SelectionBackColor=contrast?Color.White:HarborTheme.Primary;grid.DefaultCellStyle.SelectionForeColor=contrast?Color.Black:Ink;grid.ColumnHeadersDefaultCellStyle.BackColor=contrast?Color.Black:Card;grid.ColumnHeadersDefaultCellStyle.ForeColor=contrast?Color.White:Ink;}
        float factor=largeText.Checked?1.16f:1;var font=new Font("Yu Gothic UI",12*factor);foreach(var area in history.ChartAreas){area.AxisX.LabelStyle.Font=area.AxisY.LabelStyle.Font=area.AxisY2.LabelStyle.Font=font;area.AxisY.TitleFont=area.AxisY2.TitleFont=font;area.AxisX.LineColor=area.AxisY.LineColor=area.AxisY2.LineColor=contrast?Color.White:Muted;area.AxisY.MajorGrid.LineColor=contrast?Color.Gray:HarborTheme.Border;area.AxisY.TitleForeColor=contrast?Color.White:HarborTheme.Live;area.AxisY2.TitleForeColor=contrast?Color.White:HarborTheme.Blue;}
        foreach(var legend in history.Legends){legend.Font=font;legend.BackColor=contrast?Color.Black:Card;}foreach(var title in history.Titles)title.Font=font;
        history.Series[0].Color=contrast?Color.White:HarborTheme.Live;history.Series[1].Color=contrast?Color.Yellow:HarborTheme.Blue;
    }
    void ArrangeHarbor()
    {
        if(harborMain==null||arrangingHarbor)return;arrangingHarbor=true;
        try{
            float dpi=DeviceDpi/96f;float factor=(largeText!=null&&largeText.Checked?1.16f:1)*dpi;
            harborBody.ColumnStyles[0].Width=256*dpi;harborShell.RowStyles[0].Height=60*dpi;harborShell.RowStyles[2].Height=34*factor;
            bool compact=harborMain.ClientSize.Width<980*factor;
            bool dashboard=workspace.SelectedIndex<=0;headerActions.Visible=dashboard;pageHeading.Visible=dashboard;
            harborMain.RowStyles[0].Height=(dashboard?(compact?158:112):96)*factor;
            harborHeader.SetCellPosition(headerActions,new TableLayoutPanelCellPosition(compact?0:1,compact?1:0));
            harborHeader.SetColumnSpan(headerActions,compact?2:1);
            harborHeader.SetColumnSpan(harborHeader.Controls[0],compact||!dashboard?2:1);
            harborHeader.RowStyles[1].Height=compact&&dashboard?58*factor:0;
            var titles=(TableLayoutPanel)harborHeader.Controls[0];titles.RowStyles[0].Height=dashboard?26*factor:0;titles.RowStyles[1].Height=48*factor;
            for(int col=1;col<4;col++)addressRow.ColumnStyles[col].Width=new[]{112,104,192}[col-1]*factor;
            sidebarLayout.RowStyles[0].Height=100*factor;sidebarLayout.RowStyles[2].Height=88*factor;
            sidebarLayout.RowStyles[3].Height=sidebarLayout.ClientSize.Height<740*factor||highContrast.Checked?0:144;
            sidebarArt.Visible=sidebarLayout.RowStyles[3].Height>0;
            bool stacked=workspace.ClientSize.Width<1000*factor;
            var logs=historyAndLogs.Controls[1];historyAndLogs.SetCellPosition(logs,new TableLayoutPanelCellPosition(stacked?0:1,stacked?1:0));historyAndLogs.SetColumnSpan(logs,stacked?2:1);historyAndLogs.SetColumnSpan(historyAndLogs.Controls[0],stacked?2:1);
            historyAndLogs.RowStyles[1].Height=stacked?230*factor:0;
            dashboardRows.RowStyles[0].Height=(factor/dpi>1&&stacked?224:168)*factor;dashboardRows.RowStyles[1].Height=104*factor;dashboardRows.RowStyles[2].Height=224*factor;dashboardRows.RowStyles[3].Height=(stacked?650:378)*factor;
            dashboardRows.Height=(int)dashboardRows.RowStyles.Cast<RowStyle>().Sum(r=>r.Height)+8;
            foreach(var page in workspace.TabPages.Cast<TabPage>()){FitContentWidth(page,Math.Max(200,workspace.ClientSize.Width-36));FitFlowRows(page);}
            if(workspace.SelectedIndex==1){var wrap=(TableLayoutPanel)workspace.TabPages[1].Controls[0];var header=(TableLayoutPanel)wrap.Controls[0];var filters=(FlowLayoutPanel)header.Controls[0];var searches=(FlowLayoutPanel)header.Controls[1];filters.WrapContents=true;int filterHeight=RequiredFlowHeight(filters),searchHeight=RequiredFlowHeight(searches);header.RowStyles[0].Height=filterHeight;wrap.RowStyles[0].Height=filterHeight+searchHeight+12;}
            foreach(int index in new[]{2,4,6}){var page=workspace.TabPages[index];var table=(TableLayoutPanel)page.Controls[0];page.AutoScroll=true;table.Dock=DockStyle.Top;table.Height=Math.Max(page.ClientSize.Height-page.Padding.Vertical-4,(int)table.RowStyles.Cast<RowStyle>().Where(r=>r.SizeType==SizeType.Absolute).Sum(r=>r.Height)+(int)(150*factor));}
        }finally{arrangingHarbor=false;}
    }

    // Limit natural-width form controls to their available column, retaining vertical scrolling.
    void FitContentWidth(Control parent,int available)
    {
        var parentFlow=parent as FlowLayoutPanel;
        int room=parentFlow!=null&&parentFlow.Dock==DockStyle.Fill?Math.Max(180,parent.ClientSize.Width-parent.Padding.Horizontal-24):available;
        foreach(Control child in parent.Controls){
            int childRoom=Math.Max(120,room-child.Margin.Horizontal);
            var frame=child as InputFrame;var label=child as Label;var check=child as HarborCheckBox;var flow=child as FlowLayoutPanel;
            if(parentFlow!=null){
                if(parentFlow.FlowDirection==FlowDirection.LeftToRight&&!parentFlow.WrapContents){
                    var heading=parentFlow.Controls.OfType<Label>().FirstOrDefault();
                    if(heading!=null&&child!=heading)childRoom=Math.Max(120,childRoom-heading.Width-heading.Margin.Horizontal);
                }
                if(frame!=null&&frame.Dock==DockStyle.None){int width;if(!baseWidths.TryGetValue(frame,out width)){width=frame.Width;baseWidths[frame]=width;}frame.Width=Math.Min(width,childRoom);}
                if(label!=null&&label.AutoSize)label.MaximumSize=new Size(parentFlow.FlowDirection==FlowDirection.LeftToRight&&label.MaximumSize.Width>0?Math.Min(childRoom,label.MaximumSize.Width):childRoom,0);
                if(check!=null&&check.AutoSize)check.MaximumSize=new Size(childRoom,0);
                if(flow!=null&&flow.AutoSize)flow.MaximumSize=new Size(childRoom,0);
            }
            if(frame==null)FitContentWidth(child,childRoom);
        }
    }

    int RequiredFlowHeight(FlowLayoutPanel flow)
    {
        int available=Math.Max(120,flow.ClientSize.Width-flow.Padding.Horizontal),x=0,y=0,line=0;
        foreach(Control item in flow.Controls){if(!item.Visible)continue;int width=item.Width+item.Margin.Horizontal,height=item.Height+item.Margin.Vertical;if(x>0&&x+width>available){y+=line;x=0;line=0;}x+=width;line=Math.Max(line,height);}
        return y+line+flow.Padding.Vertical+4;
    }
    void FitFlowRows(Control parent)
    {
        foreach(Control child in parent.Controls){
            var label=child as Label;var owner=parent as TableLayoutPanel;
            if(label!=null&&owner!=null&&label.Visible){int row=owner.GetRow(label);if(row>=0&&row<owner.RowStyles.Count&&owner.RowStyles[row].SizeType==SizeType.Absolute){int needed=label.AutoEllipsis?label.Font.Height:label.GetPreferredSize(new Size(Math.Max(60,label.Width),0)).Height;owner.RowStyles[row].Height=Math.Max(owner.RowStyles[row].Height,needed+label.Margin.Vertical);}}
            var flow=child as FlowLayoutPanel;
            if(flow!=null&&flow.FlowDirection==FlowDirection.LeftToRight&&flow.Controls.OfType<Button>().Any()){
                flow.WrapContents=true;
                int needed=RequiredFlowHeight(flow);var table=flow.Parent as TableLayoutPanel;
                if(table!=null&&flow.Dock==DockStyle.Fill){int row=table.GetRow(flow);if(row>=0&&row<table.RowStyles.Count&&table.RowStyles[row].SizeType==SizeType.Absolute)table.RowStyles[row].Height=Math.Max(table.RowStyles[row].Height,needed);}
                if(flow.Dock==DockStyle.Bottom)flow.Height=Math.Max(flow.Height,needed);
            }FitFlowRows(child);}
    }
}
