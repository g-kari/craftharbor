using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

static class SurfacePaint
{
    public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=Math.Max(1,radius*2);p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    public static void Background(Control control,Graphics g,bool wood){g.Clear(control.BackColor);HarborTheme.Frame(g,control.ClientRectangle,wood,HarborTheme.Contrast(control));}
}
class SurfacePanel:Panel
{
    public bool WoodFrame;
    public SurfacePanel(){DoubleBuffered=true;}
    protected override void OnPaintBackground(PaintEventArgs e){SurfacePaint.Background(this,e.Graphics,WoodFrame);}
}
class SurfaceTable:TableLayoutPanel
{
    public bool WoodFrame;
    public SurfaceTable(){DoubleBuffered=true;}
    protected override void OnPaintBackground(PaintEventArgs e){SurfacePaint.Background(this,e.Graphics,WoodFrame);}
}
