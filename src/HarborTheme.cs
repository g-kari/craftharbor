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
    static Image lighthouse,harbor,atlas;
    public static Image IconAtlas{get{return atlas??(atlas=Load("CraftHarbor.icons.png"));}}
    public static Image Lighthouse{get{return lighthouse??(lighthouse=Load("CraftHarbor.lighthouse.png"));}}
    public static Image Harbor{get{return harbor??(harbor=Load("CraftHarbor.harbor.png"));}}
    static Image Load(string name){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)){if(stream==null)return null;using(var source=Image.FromStream(stream))return new Bitmap(source);}}
    public static bool Contrast(Control c){return SystemInformation.HighContrast||c.BackColor==Color.Black;}
    public static void Frame(Graphics g,Rectangle bounds,bool wood,bool contrast)
    {
        if(bounds.Width<12||bounds.Height<12)return;g.SmoothingMode=SmoothingMode.None;
        Color edge=contrast?Color.White:Border;
        using(var p=new Pen(edge)){g.DrawRectangle(p,bounds.X+1,bounds.Y+1,bounds.Width-3,bounds.Height-3);}
        if(!wood||contrast)return;
        int l=bounds.Left,t=bounds.Top,r=bounds.Right-1,b=bounds.Bottom-1;
        using(var dark=new Pen(Color.FromArgb(62,38,20)))using(var middle=new Pen(Wood,2))using(var light=new Pen(Color.FromArgb(224,164,82))){
            g.DrawRectangle(dark,l,t,r-l,b-t);g.DrawLine(middle,l+4,t+2,r-4,t+2);g.DrawLine(middle,l+4,b-2,r-4,b-2);
            g.DrawLine(light,l+5,t+1,r-5,t+1);g.DrawLine(light,l+5,b-3,r-5,b-3);
            // Deterministic short grain marks: no full-screen bitmap or random repaint noise.
            for(int x=l+18;x<r-14;x+=23){g.DrawLine(dark,x,t+3,Math.Min(x+8,r-14),t+3);g.DrawLine(dark,x+5,b-1,Math.Min(x+13,r-14),b-1);}
        }
        foreach(var corner in new[]{new Point(l,t),new Point(r-11,t),new Point(l,b-11),new Point(r-11,b-11)}){
            bool right=corner.X>l,bottom=corner.Y>t;int x=corner.X,y=corner.Y;
            using(var shadow=new SolidBrush(Color.FromArgb(44,29,19)))g.FillRectangle(shadow,x,y,12,12);
            using(var fill=new SolidBrush(Wood)){g.FillRectangle(fill,x+1,y+1,10,4);g.FillRectangle(fill,x+(right?7:1),y+1,4,10);}
            using(var shine=new Pen(Color.FromArgb(241,184,99))){g.DrawLine(shine,x+2,y+1,x+9,y+1);g.DrawLine(shine,x+(right?10:1),y+2,x+(right?10:1),y+9);}
            using(var nail=new SolidBrush(Color.FromArgb(70,44,23)))g.FillRectangle(nail,x+(right?8:3),y+(bottom?8:3),2,2);
        }
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
    public bool DivideColumns;
    public HarborFrame(){WoodFrame=true;Padding=new Padding(12);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(!DivideColumns)return;var widths=GetColumnWidths();int x=Padding.Left;using(var pen=new Pen(HarborTheme.Contrast(this)?Color.White:HarborTheme.Border)){for(int i=0;i<widths.Length-1;i++){x+=widths[i];e.Graphics.DrawLine(pen,x-1,Padding.Top,x-1,Height-Padding.Bottom);}}}
}

class HarborGlyph:Control
{
    public string IconKey="server";
    public HarborGlyph(){DoubleBuffered=true;TabStop=false;AccessibleRole=AccessibleRole.Graphic;SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;}
    protected override void OnPaint(PaintEventArgs e){int side=Math.Min(Width,Height);Icons.Draw(e.Graphics,IconKey,new Rectangle((Width-side)/2,(Height-side)/2,side,side),HarborTheme.Contrast(this)?Color.White:ForeColor);}
}
class HarborWordmark:Control
{
    static readonly string[] Letters={
        "01111/11000/11000/11000/11000/11000/01111", // C
        "00000/00000/11011/11100/11000/11000/11000", // r
        "00000/00000/01110/00011/01111/11011/01111", // a
        "00111/01100/01100/11110/01100/01100/01100", // f
        "01100/01100/11110/01100/01100/01101/00110", // t
        "11011/11011/11011/11111/11011/11011/11011", // H
        "00000/00000/01110/00011/01111/11011/01111",
        "00000/00000/11011/11100/11000/11000/11000",
        "11000/11000/11110/11011/11011/11011/11110",
        "00000/00000/01110/11011/11011/11011/01110",
        "00000/00000/11011/11100/11000/11000/11000"};
    public HarborWordmark(){DoubleBuffered=true;TabStop=false;AccessibleRole=AccessibleRole.StaticText;AccessibleName="CraftHarbor";}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);float s=Math.Min(Height/10f,Width/67f);s=Math.Max(1,(float)Math.Floor(s));int x=0,y=(int)((Height-7*s)/2);using(var brush=new SolidBrush(ForeColor)){foreach(var glyph in Letters){var rows=glyph.Split('/');for(int row=0;row<7;row++)for(int col=0;col<5;col++)if(rows[row][col]=='1')e.Graphics.FillRectangle(brush,x+col*s,y+row*s,s,s);x+=(int)(6*s);}}}
}
