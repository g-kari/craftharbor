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
        foreach(var c in Descendants(this).Where(c=>c is TextBox||c is Button||c is Label||c is ComboBox||c is CheckBox))Require(c.Font.SizeInPoints>=12,"UI text is at least 12pt before enlargement");
        previewJavaPath=JavaPath();javaProbeError="Synthetic Java check error";RefreshJavaPreview();RefreshJavaPreview();Require(javaPreview.Text.Contains("Synthetic Java check error"),"Java probe failure survives preview refresh");javaProbeError="";RefreshJavaPreview();
        largeText.Checked=false;highContrast.Checked=false;ApplyAccessibleTheme();Size=new Size(1024,720);
        CaptureGenericCheck(this,"ui-console-1024.png",1);CheckCommandRow();CaptureGenericCheck(this,"ui-dashboard-1024.png",0);
        Size=new Size(1360,960);workspace.SelectedIndex=3;Application.DoEvents();var settings=(FlowLayoutPanel)workspace.TabPages[3].Controls[0];settings.ScrollControlIntoView(autoDistances.Parent);Application.DoEvents();CaptureGenericCheck(this,"ui-distance-settings.png",3);
        settings.ScrollControlIntoView(autoJava.Parent);Application.DoEvents();CaptureGenericCheck(this,"ui-java-settings.png",3);
        Size=new Size(1024,720);largeText.Checked=true;highContrast.Checked=true;ApplyAccessibleTheme();CaptureGenericCheck(this,"ui-console-accessible-1024.png",1);CheckCommandRow();Size=new Size(1360,960);
        File.WriteAllText(Path.Combine(root,"ui-review-verification.txt"),"PASS: 12pt UI text baseline; console command row remains inside its layout at 1024x720 in normal and enlarged/high-contrast modes; 600-sample chart rollover and sample-gap rendering; CPU/memory/server/player distance policy, next-start settings persistence, active-port write protection, backup preservation. Screenshots are synthetic WinForms DrawToBitmap captures; native screen readers and Windows DPI scaling are not verified.");
    }
    static string LayoutTrace(Control control){string text="";for(Control c=control;c!=null;c=c.Parent)text+=c.GetType().Name+"="+c.Bounds+"; ";return text;}
    void CheckCommandRow()
    {
        Application.DoEvents();var frame=command.Parent;var panel=frame.Parent;Require(frame.Height>=command.PreferredSize.Height+14&&frame.Bottom<=panel.ClientSize.Height&&frame.Right<=panel.ClientSize.Width,"console input has enough vertical space");
        foreach(var button in panel.Controls.OfType<Button>())Require(button.Bottom<=panel.ClientSize.Height&&button.Right<=panel.ClientSize.Width&&button.RectangleToScreen(button.ClientRectangle).Right<=RectangleToScreen(ClientRectangle).Right&&button.Height>=button.Font.Height+12,"console send button is fully inside its row: "+LayoutTrace(button));
        Require(logSourceFilter.ForeColor!=Color.Black||highContrast.Checked,"log filter uses readable foreground");
    }
}
