using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

partial class CraftHarbor
{
    [DllImport("user32.dll",EntryPoint="SetWindowPos",SetLastError=true)]
    static extern bool SetReviewWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    void SetReviewSize(Size size)
    {
        // A hosted runner's small desktop caps Form.Size in SetBoundsCore. Size this
        // synthetic window directly; no display settings or production window behavior changes.
        Require(SetReviewWindowPos(Handle,IntPtr.Zero,0,0,size.Width,size.Height,0x416),"native review window sizing");Application.DoEvents();ArrangeHarbor();
        Require(Width==size.Width&&Height==size.Height,"requested native capture size "+size+", actual "+Size);
    }
    void CaptureHarborPage(string name,int page)
    {
        File.AppendAllText(Path.Combine(root,"ui-check-progress.txt"),name+"\n");workspace.SelectedIndex=page;workspace.TabPages[page].AutoScrollPosition=Point.Empty;
        var form=workspace.TabPages[page].Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
        if(form!=null)form.AutoScrollPosition=Point.Empty;
        ArrangeHarbor();Application.DoEvents();CaptureGenericCheck(this,name,page);
        if(page==0){Require(heapPreview.Height>=heapPreview.Font.Height,"memory summary readable: "+LayoutTrace(heapPreview));Require(hostingSummary.Height>=hostingSummary.Font.Height,"provider summary readable: "+LayoutTrace(hostingSummary));}
        Require(dashboardTitle.Height>=dashboardTitle.Font.Height,"page title fits: "+LayoutTrace(dashboardTitle));
    }
    void CheckHarborScreens()
    {
        Require(HarborTheme.Lighthouse!=null&&HarborTheme.Harbor!=null,"promotional artwork embedded");
        largeText.Checked=false;highContrast.Checked=false;ApplyAccessibleTheme();
        foreach(var size in new[]{new Size(1360,960),new Size(1024,720)}){
            SetReviewSize(size);
            for(int page=0;page<9;page++){navigation[page].PerformClick();Require(workspace.SelectedIndex==page,"navigation routes to page");CaptureHarborPage("ui-harbor-"+size.Width+"-"+page+".png",page);}
        }
        OnKeyDown(new KeyEventArgs(Keys.Alt|Keys.D9));Require(workspace.SelectedIndex==8,"Alt+9 navigation");
        OnKeyDown(new KeyEventArgs(Keys.Control|Keys.L));Require(workspace.SelectedIndex==1&&command.CanSelect,"Ctrl+L console route and selectable command input");
        OnKeyDown(new KeyEventArgs(Keys.F1));Require(workspace.SelectedIndex==7,"F1 participant guide");
        navigation[0].Focus();Require(SelectNextControl(navigation[0],true,true,true,true)&&ActiveControl!=null,"Tab-order navigation");
        string provider=hosting.Provider;
        foreach(var method in new[]{"playit","ngrok","direct","port-tool"}){
            hosting.Provider=method;connectionStatus="未設定";ApplyDiscoveredAddresses();
            foreach(bool sr in new[]{false,true})foreach(bool tr in new[]{false,true}){
                ready=sr;RefreshDashboardPresentation(sr,tr);
                Require(start.Enabled==!sr&&stop.Enabled==sr,"independent server controls");
                Require(connect.Enabled==!tr&&disconnect.Enabled==tr,"independent tunnel controls");
                Require(footerState.Text.Contains(HostingName),"configured provider shown");
            }
        }
        hosting.Provider=provider;ready=false;RefreshDashboardPresentation(false,true);
        connectionStatus="合成データ：公開接続だけ実行中";ApplyDiscoveredAddresses();
        SetReviewSize(new Size(1536,1024));CaptureHarborPage("ui-harbor-independent.png",0);
        Require(player.Parent.Parent==workspace.TabPages[2].Controls[0].Controls.Cast<Control>().First(c=>c.Contains(player)),"whitelist remains in participant layout");
        logRecords.Clear();RebuildLogView();Require(recentLogs.Text.Contains("まだログはありません"),"empty recent logs explained");
        AppendLogRecords(new[]{"[MC] Cloud UI fixture: world saved","[公開] Cloud UI fixture: tunnel remains running","[管理] Cloud UI fixture: no real server is started"});
        logSourceFilter.SelectedIndex=1;Require(recentLogs.Text.Contains("tunnel remains running")&&!log.Text.Contains("tunnel remains running"),"recent logs share console history independently of its filter");logSourceFilter.SelectedIndex=0;
        SetReviewSize(new Size(1024,720));largeText.Checked=true;highContrast.Checked=true;ApplyAccessibleTheme();
        for(int page=0;page<9;page++){CaptureHarborPage("ui-harbor-contrast-"+page+".png",page);}
        Require(history.Legends[0].Font.SizeInPoints>=13.9f,"large text includes chart legend");
        Require(navigation.All(b=>b.ForeColor==Color.White),"high contrast navigation");
        Require(Descendants(this).Where(c=>c is Button||c is Label||c is TextBox||c is ComboBox).All(c=>c.Font.SizeInPoints>=12),"12pt floor retained");
        workspace.SelectedIndex=0;workspace.TabPages[0].ScrollControlIntoView(historyAndLogs);Application.DoEvents();CaptureGenericCheck(this,"ui-harbor-contrast-history.png",0);
        File.AppendAllText(Path.Combine(root,"ui-check-progress.txt"),"High contrast dialogs\n");OpenServersDialog(true);HostingSettings(true);ShowPackImport(this,root,"",true);
        foreach(var name in new[]{"ui-servers","ui-hosting","ui-import"})File.Copy(Path.Combine(root,name+".png"),Path.Combine(root,name+"-contrast.png"),true);
        largeText.Checked=false;highContrast.Checked=false;ApplyAccessibleTheme();SetReviewSize(new Size(1360,960));
        preparingServer=true;RefreshDashboardPresentation(false,false);Require(!start.Enabled&&serverState.Text.Contains("確認中"),"preparing disables game start");preparingServer=false;
        ready=false;RefreshDashboardPresentation(true,false);Require(!start.Enabled&&stop.Enabled&&serverState.Text.Contains("起動中"),"starting game independent from stopped tunnel");
        stopping=true;RefreshDashboardPresentation(true,true);Require(!stop.Enabled&&disconnect.Enabled,"saving game leaves tunnel stop usable");stopping=false;
        connectionStatus="接続先の取得に失敗しました";ApplyDiscoveredAddresses();Require(hostingSummary.Text.Contains("失敗"),"public error remains visible");
        RefreshDashboardPresentation(false,false);UpdateFeatures(false,false);Require(cpuValue.Text=="—"&&tpsValue.Text=="—"&&history.Titles["state"].Text.Contains("最後"),"stopped metrics and historical data labeled");
        workspace.SelectedIndex=0;ArrangeHarbor();workspace.TabPages[0].ScrollControlIntoView(historyAndLogs);Application.DoEvents();CaptureGenericCheck(this,"ui-harbor-history.png",0);
        MinimumSize=new Size(1024,720);
        File.WriteAllText(Path.Combine(root,"harbor-review-verification.txt"),"PASS: exact-size native captures; navigation routing; startup/saving/failed/empty states; whitelist layout; embedded artwork; 9-page normal/narrow and enlarged/high-contrast rendering; independent server/tunnel controls for all four providers; provider-specific display; recent-log empty state/shared buffer/filter independence; 12pt minimum and enlarged chart legend. Captures are synthetic native WinForms controls, not real Minecraft/tunnel processes. Physical high-DPI monitors, native screen-reader output, foreground keystroke delivery and interactive OS title-bar transitions were not exercised.");
    }
}
