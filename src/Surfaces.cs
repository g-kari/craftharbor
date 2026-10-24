using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

static class SurfacePaint
{
    public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    public static void Background(Control control,Graphics g){g.Clear(control.Parent==null?control.BackColor:control.Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Round(new RectangleF(0,0,control.Width-1,control.Height-1),12))using(var fill=new SolidBrush(control.BackColor))using(var border=new Pen(control.BackColor==Color.Black?Color.White:Color.FromArgb(48,62,76))){g.FillPath(fill,path);g.DrawPath(border,path);}}
}
class SurfacePanel:Panel
{
    public SurfacePanel(){DoubleBuffered=true;}
    protected override void OnPaintBackground(PaintEventArgs e){SurfacePaint.Background(this,e.Graphics);}
}
class SurfaceTable:TableLayoutPanel
{
    public SurfaceTable(){DoubleBuffered=true;}
    protected override void OnPaintBackground(PaintEventArgs e){SurfacePaint.Background(this,e.Graphics);}
}
