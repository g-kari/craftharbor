using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Shared palette and decorations. Decorations never contain labels or controls.
static class HarborTheme
{
    public static readonly Color Header=ColorTranslator.FromHtml("#F7EED8"), Background=ColorTranslator.FromHtml("#14212A"),
        Panel=ColorTranslator.FromHtml("#1E303A"), Primary=ColorTranslator.FromHtml("#345F2C"),
        Wood=ColorTranslator.FromHtml("#A67C4A"), Text=ColorTranslator.FromHtml("#F2F0E6"),
        Muted=ColorTranslator.FromHtml("#BBCBD0"), Border=ColorTranslator.FromHtml("#506976"),
        Focus=ColorTranslator.FromHtml("#F2D28A"), Live=ColorTranslator.FromHtml("#9DDB82"),
        Input=ColorTranslator.FromHtml("#10202A"), Blue=ColorTranslator.FromHtml("#76B9F5");
    public const int Gap=8;
    static Image lighthouse,harbor;
    public static Image Lighthouse{get{return lighthouse??(lighthouse=Load("CraftHarbor.lighthouse.png"));}}
    public static Image Harbor{get{return harbor??(harbor=Load("CraftHarbor.harbor.png"));}}
    static Image Load(string name){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)){if(stream==null)return null;using(var source=Image.FromStream(stream))return new Bitmap(source);}}
    public static bool Contrast(Control c){return SystemInformation.HighContrast||c.BackColor==Color.Black;}
    public static void Frame(Graphics g,Rectangle bounds,bool wood,bool contrast)
    {
        if(bounds.Width<8||bounds.Height<8)return;
        g.SmoothingMode=SmoothingMode.None;
        using(var p=new Pen(contrast?Color.White:wood?Wood:Border)){g.DrawRectangle(p,bounds.X,bounds.Y,bounds.Width-1,bounds.Height-1);}
        if(!wood||contrast)return;
        using(var light=new Pen(Color.FromArgb(205,160,97)))using(var dark=new Pen(Color.FromArgb(76,52,30))){
            g.DrawLine(light,bounds.Left+4,bounds.Top+1,bounds.Right-5,bounds.Top+1);
            g.DrawLine(dark,bounds.Left+4,bounds.Bottom-2,bounds.Right-5,bounds.Bottom-2);
        }
        using(var b=new SolidBrush(Wood)){foreach(var p in new[]{new Point(bounds.Left,bounds.Top),new Point(bounds.Right-7,bounds.Top),new Point(bounds.Left,bounds.Bottom-7),new Point(bounds.Right-7,bounds.Bottom-7)}){g.FillRectangle(b,p.X,p.Y,7,7);}}
    }
}
class HarborArtwork:Control
{
    public bool LogoOnly;
    public HarborArtwork(){DoubleBuffered=true;TabStop=false;AccessibleRole=AccessibleRole.Graphic;AccessibleName="CraftHarborの灯台と港";SetStyle(ControlStyles.SupportsTransparentBackColor,true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);
        if(HarborTheme.Contrast(this))return;
        Image image=LogoOnly?HarborTheme.Lighthouse:HarborTheme.Harbor;
        if(image==null)return;
        float ratio=Math.Min(Width/(float)image.Width,Height/(float)image.Height);
        var r=new Rectangle((Width-(int)(image.Width*ratio))/2,Height-(int)(image.Height*ratio),(int)(image.Width*ratio),(int)(image.Height*ratio));
        e.Graphics.InterpolationMode=InterpolationMode.NearestNeighbor;e.Graphics.PixelOffsetMode=PixelOffsetMode.Half;e.Graphics.DrawImage(image,r);
    }
}
class HarborFrame:SurfaceTable
{
    public HarborFrame(){WoodFrame=true;Padding=new Padding(12);}
}
