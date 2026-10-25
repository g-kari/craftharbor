using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

partial class CraftHarbor
{
    // Called only by existing --experience-check fixtures. Never invokes a diagnostic probe or an update action.
    void CaptureOperationsDialog(Form dialog,string file,Action check=null)
    {
        if(highContrast!=null&&highContrast.Checked)file=Path.GetFileNameWithoutExtension(file)+"-contrast.png";
        dialog.Shown+=delegate{dialog.BeginInvoke((Action)delegate{
            if(check!=null)check();Application.DoEvents();
            foreach(Control control in Descendants(dialog)){
                var button=control as Button;if(button!=null)Require(button.Height>=button.Font.Height+12,"operation dialog buttons have readable height");
                Require(control.Font.SizeInPoints>=12,"operation dialog text is at least 12pt");
            }
            using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(root,file),System.Drawing.Imaging.ImageFormat.Png);}
            dialog.Width=Math.Max(dialog.MinimumSize.Width,760);Application.DoEvents();
            using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(root,Path.GetFileNameWithoutExtension(file)+"-narrow.png"),System.Drawing.Imaging.ImageFormat.Png);}
            dialog.Close();
        });};
    }
    void CaptureOperationsScreens()
    {
        string path=Path.Combine(ServerDir,"server.properties"),before=File.ReadAllText(path);
        bool wasLarge=largeText.Checked,wasContrast=highContrast.Checked;
        try{
            largeText.Checked=false;highContrast.Checked=false;ApplyAccessibleTheme();
            ShowConnectionDiagnostics(true);ShowModCompatibility(true);ShowUpdateSandbox(true);
            largeText.Checked=true;highContrast.Checked=true;ApplyAccessibleTheme();
            ShowConnectionDiagnostics(true);ShowModCompatibility(true);ShowUpdateSandbox(true);
        }finally{largeText.Checked=wasLarge;highContrast.Checked=wasContrast;ApplyAccessibleTheme();}
        Require(File.ReadAllText(path)==before,"operation dialog previews do not alter server properties");
        File.WriteAllText(Path.Combine(root,"operations-ui-verification.txt"),"PASS: synthetic connection-diagnostic, participant-MOD and update-sandbox dialogs rendered in large-text/high-contrast and narrow sizes; MOD table sorting preserves ID/role mapping; preview close returns without server-property changes. No probes, Java/server/tunnel startup, update copy, promotion, network calls or real workspace used. Pixel images require visual review; these checks do not prove keyboard or screen-reader completeness.");
    }
}
