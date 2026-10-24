using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

partial class CraftHarbor
{
    void CheckHarborScreens()
    {
        Require(HarborTheme.Lighthouse!=null&&HarborTheme.Harbor!=null,"promotional artwork embedded");
        largeText.Checked=false;highContrast.Checked=false;ApplyAccessibleTheme();
        foreach(var size in new[]{new Size(1360,960),new Size(1024,720)}){
            Size=size;ArrangeHarbor();
            for(int page=0;page<9;page++){workspace.SelectedIndex=page;ArrangeHarbor();Application.DoEvents();CaptureGenericCheck(this,"ui-harbor-"+size.Width+"-"+page+".png",page);}
        }
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
        Size=new Size(1360,960);workspace.SelectedIndex=0;ArrangeHarbor();CaptureGenericCheck(this,"ui-harbor-independent.png",0);
        logRecords.Clear();RebuildLogView();Require(recentLogs.Text.Contains("まだログはありません"),"empty recent logs explained");
        AppendLogRecords(new[]{"[MC] Cloud UI fixture: world saved","[公開] Cloud UI fixture: tunnel remains running","[管理] Cloud UI fixture: no real server is started"});
        logSourceFilter.SelectedIndex=1;Require(recentLogs.Text.Contains("tunnel remains running")&&!log.Text.Contains("tunnel remains running"),"recent logs share console history independently of its filter");logSourceFilter.SelectedIndex=0;
        Size=new Size(1024,720);largeText.Checked=true;highContrast.Checked=true;ApplyAccessibleTheme();
        for(int page=0;page<9;page++){workspace.SelectedIndex=page;ArrangeHarbor();Application.DoEvents();CaptureGenericCheck(this,"ui-harbor-contrast-"+page+".png",page);}
        Require(history.Legends[0].Font.SizeInPoints>=13.9f,"large text includes chart legend");
        Require(navigation.All(b=>b.ForeColor==Color.White),"high contrast navigation");
        Require(Descendants(this).Where(c=>c is Button||c is Label||c is TextBox||c is ComboBox).All(c=>c.Font.SizeInPoints>=12),"12pt floor retained");
        largeText.Checked=false;highContrast.Checked=false;ApplyAccessibleTheme();Size=new Size(1360,960);RefreshDashboardPresentation(false,false);ArrangeHarbor();
        File.WriteAllText(Path.Combine(root,"harbor-review-verification.txt"),"PASS: embedded artwork; 9-page normal/narrow and enlarged/high-contrast rendering; independent server/tunnel controls for all four providers; provider-specific display; recent-log empty state/shared buffer/filter independence; 12pt minimum and enlarged chart legend. Captures are synthetic native WinForms controls, not real Minecraft/tunnel processes. Physical high-DPI monitors, native screen-reader output and interactive OS title-bar transitions were not exercised.");
    }
}
