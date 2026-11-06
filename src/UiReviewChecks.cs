using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

partial class CraftHarbor
{
    void ObserveKeyboardReview(Control container)
    {
        if(ManagerDataOverride==null||Array.IndexOf(Environment.GetCommandLineArgs(),"--keyboard-review")<0)return;
        foreach(var control in new[]{container}.Concat(Descendants(container))){var observed=control;observed.GotFocus+=delegate{File.AppendAllText(Path.Combine(root,"keyboard-focus.txt"),DateTime.UtcNow.ToString("o")+" | "+observed.GetType().Name+" | "+observed.AccessibleName+Environment.NewLine);};}
    }
    void CheckReviewScreens()
    {
        CheckLogRecordBoundaries();
        foreach(var c in Descendants(this).Where(c=>c is TextBox||c is Button||c is Label||c is ComboBox||c is CheckBox))Require(c.Font.SizeInPoints>=12,"UI text is at least 12pt before enlargement");
        previewJavaPath=JavaPath();javaProbeError="Synthetic Java check error";RefreshJavaPreview();RefreshJavaPreview();Require(javaPreview.Text.Contains("Synthetic Java check error"),"Java probe failure survives preview refresh");javaProbeError="";RefreshJavaPreview();
        largeText.Checked=false;highContrast.Checked=false;ApplyAccessibleTheme();Size=new Size(1024,720);
        CaptureGenericCheck(this,"ui-console-1024.png",1);CheckCommandRow();CaptureGenericCheck(this,"ui-dashboard-1024.png",0);
        Size=new Size(1360,960);workspace.SelectedIndex=3;Application.DoEvents();var settings=(FlowLayoutPanel)workspace.TabPages[3].Controls[0];settings.ScrollControlIntoView(autoDistances.Parent);Application.DoEvents();CaptureGenericCheck(this,"ui-distance-settings.png",3);
        settings.ScrollControlIntoView(autoJava.Parent);Application.DoEvents();CaptureGenericCheck(this,"ui-java-settings.png",3);
        Size=new Size(1024,720);largeText.Checked=true;highContrast.Checked=true;ApplyAccessibleTheme();CaptureGenericCheck(this,"ui-console-accessible-1024.png",1);CheckCommandRow();Size=new Size(1360,960);
        File.WriteAllText(Path.Combine(root,"ui-review-verification.txt"),"PASS: log record boundaries after source filtering, empty results, 4000-record rollover and 500000-character trimming; 12pt UI text baseline; console command row remains inside its layout at 1024x720 in normal and enlarged/high-contrast modes; 600-sample chart rollover and sample-gap rendering; CPU/memory/server/player distance policy, next-start settings persistence, active-port write protection, backup preservation. Screenshots are synthetic WinForms DrawToBitmap captures; native screen readers and Windows DPI scaling are not verified.");
    }
    void RequireLogView(string[] expected,int total,string context)
    {
        if(expected.Length==0)Require(log.Text=="",context+": no text or blank line without visible records");
        else{
            var actual=log.Text.Split(new[]{Environment.NewLine},StringSplitOptions.None);
            Require(actual.Length==expected.Length+1&&actual[actual.Length-1]=="",context+": one final record delimiter and no extra blank line");
            Require(actual.Take(expected.Length).SequenceEqual(expected),context+": visible records retain their content, order and separation");
        }
        Require(logCounter.Text==expected.Length+" / "+total+"件を表示",context+": visible and retained counts");
    }
    void CheckLogRecordBoundaries()
    {
        var savedRecords=logRecords.ToArray();int savedFilter=logSourceFilter.SelectedIndex;string savedText=log.Text;int savedStart=log.SelectionStart,savedLength=log.SelectionLength;
        string savedRecent=recentLogs.Text;int savedRecentStart=recentLogs.SelectionStart,savedRecentLength=recentLogs.SelectionLength;
        string preference=Path.Combine(root,"manager-log-filter.txt");byte[] savedPreference=File.Exists(preference)?File.ReadAllBytes(preference):null;
        try{
            logRecords.Clear();logSourceFilter.SelectedIndex=0;RebuildLogView();
            RequireLogView(new string[0],0,"empty rebuild");AppendLogRecords(new string[0]);RequireLogView(new string[0],0,"empty append");
            var mixed=new[]{"[MC] Synthetic game before filter","[公開] Synthetic public before filter","[管理] Synthetic manager before filter"};
            AppendLogRecords(mixed);RequireLogView(mixed,3,"initial mixed append");
            logSourceFilter.SelectedIndex=1;RequireLogView(new[]{mixed[0]},3,"game filter rebuild");
            AppendLogRecords(new[]{"[管理] Synthetic hidden append","[MC] Synthetic game after filter"});
            RequireLogView(new[]{mixed[0],"[MC] Synthetic game after filter"},5,"append after filter rebuild");
            logSourceFilter.SelectedIndex=0;
            RequireLogView(new[]{mixed[0],mixed[1],mixed[2],"[管理] Synthetic hidden append","[MC] Synthetic game after filter"},5,"all sources restored");

            logRecords.Clear();logSourceFilter.SelectedIndex=3;RebuildLogView();
            AppendLogRecords(new[]{"[MC] Synthetic unmatched game"});RequireLogView(new string[0],1,"no matching records");
            logSourceFilter.SelectedIndex=2;logSourceFilter.SelectedIndex=3;RequireLogView(new string[0],1,"no matching filter rebuild");
            AppendLogRecords(new[]{"[MC] Synthetic unmatched append"});RequireLogView(new string[0],2,"hidden append remains empty");
            AppendLogRecords(new[]{"[管理] Synthetic first visible record"});RequireLogView(new[]{"[管理] Synthetic first visible record"},3,"first match has no leading blank line");
            AppendLogRecords(new string[0]);RequireLogView(new[]{"[管理] Synthetic first visible record"},3,"empty batch preserves visible content");

            logRecords.Clear();logSourceFilter.SelectedIndex=0;RebuildLogView();
            var shortLines=Enumerable.Range(0,4002).Select(i=>"[MC] Synthetic retained line "+i).ToArray();
            AppendLogRecords(shortLines.Take(4000));RequireLogView(shortLines.Take(4000).ToArray(),4000,"at line retention limit");
            AppendLogRecords(new[]{shortLines[4000]});RequireLogView(shortLines.Skip(1).Take(4000).ToArray(),4000,"line retention rollover");
            AppendLogRecords(new[]{shortLines[4001]});RequireLogView(shortLines.Skip(2).ToArray(),4000,"append after line retention rollover");
            Require(log.TextLength<=500000,"short retained records remain within character limit");

            logRecords.Clear();RebuildLogView();
            var longLines=Enumerable.Range(0,100).Select(i=>{string prefix="[MC] Synthetic long line "+i.ToString("D3")+" ";return prefix+new string('x',5000-Environment.NewLine.Length-prefix.Length);}).ToArray();
            AppendLogRecords(longLines);RequireLogView(longLines,100,"at character retention limit");
            Require(log.TextLength==500000,"character retention limit includes each final record delimiter");
            AppendLogRecords(new[]{"[MC] Synthetic character trim trigger"});
            RequireLogView(new[]{"[MC] Synthetic character trim trigger"},1,"character trimming removes the oldest 100 records");
            Require(log.TextLength<=500000,"character trimming enforces visible text limit");
            AppendLogRecords(new[]{"[MC] Synthetic record after character trim"});
            RequireLogView(new[]{"[MC] Synthetic character trim trigger","[MC] Synthetic record after character trim"},2,"append after character trimming");
            Require(log.TextLength<=500000,"append after character trimming stays within limit");
        }finally{
            logRecords.Clear();logRecords.AddRange(savedRecords);logSourceFilter.SelectedIndex=savedFilter;RebuildLogView();log.Text=savedText;log.Select(savedStart,savedLength);
            recentLogs.Text=savedRecent;FitRecentLogScroll();recentLogs.Select(savedRecentStart,savedRecentLength);
            if(savedPreference==null)File.Delete(preference);else File.WriteAllBytes(preference,savedPreference);
        }
        Require(log.Text==savedText&&logRecords.SequenceEqual(savedRecords)&&logSourceFilter.SelectedIndex==savedFilter&&recentLogs.Text==savedRecent,"log boundary checks restore the screenshot fixture");
    }
    static string LayoutTrace(Control control){string text="";for(Control c=control;c!=null;c=c.Parent)text+=c.GetType().Name+"="+c.Bounds+"; ";return text;}
    void CheckCommandRow()
    {
        Application.DoEvents();var frame=command.Parent;var panel=frame.Parent;Require(frame.Height>=command.PreferredSize.Height+14&&frame.Bottom<=panel.ClientSize.Height&&frame.Right<=panel.ClientSize.Width,"console input has enough vertical space");
        foreach(var button in panel.Controls.OfType<Button>())Require(button.Bottom<=panel.ClientSize.Height&&button.Right<=panel.ClientSize.Width&&button.RectangleToScreen(button.ClientRectangle).Right<=RectangleToScreen(ClientRectangle).Right&&button.Height>=button.Font.Height+12,"console send button is fully inside its row: "+LayoutTrace(button));
        Require(logSourceFilter.ForeColor!=Color.Black||highContrast.Checked,"log filter uses readable foreground");
    }
}
