using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

class InputFrame:Panel
{
    readonly Control input;
    bool hover;
    public InputFrame(Control control){input=control;DoubleBuffered=true;BackColor=Color.FromArgb(22,35,47);ForeColor=control.ForeColor;Name="frame-"+control.Name;AccessibleRole=AccessibleRole.Grouping;AccessibleName=control.AccessibleName;TabStop=false;
        control.Enter+=delegate{Invalidate();};control.Leave+=delegate{Invalidate();};control.MouseEnter+=delegate{hover=true;Invalidate();};control.MouseLeave+=delegate{hover=false;Invalidate();};control.EnabledChanged+=delegate{Invalidate();};
        var text=control as TextBox;if(text!=null)text.BorderStyle=BorderStyle.None;var number=control as NumericUpDown;if(number!=null)number.BorderStyle=BorderStyle.None;control.BackColor=BackColor;Controls.Add(control);Resize+=delegate{Arrange();};FontChanged+=delegate{Arrange();};Arrange();}
    void Arrange(){if(input==null)return;bool multiline=input is TextBox&&((TextBox)input).Multiline;input.Dock=DockStyle.None;int inset=multiline?12:12;input.SetBounds(inset,multiline?10:Math.Max(7,(Height-input.PreferredSize.Height)/2),Math.Max(30,Width-inset*2),multiline?Math.Max(20,Height-20):input.PreferredSize.Height);}
    protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;bool contrast=BackColor==Color.Black;Color border=contrast?Color.White:input.ContainsFocus?Color.FromArgb(133,229,206):hover?Color.FromArgb(107,137,153):Color.FromArgb(96,120,140);using(var path=SurfacePaint.Round(new RectangleF(1,1,Width-3,Height-3),8))using(var fill=new SolidBrush(BackColor))using(var pen=new Pen(border,input.ContainsFocus?2:1)){e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}}
    public static void Wrap(Control input){if(input.Parent==null||input.Parent is InputFrame)return;var parent=input.Parent;int index=parent.Controls.GetChildIndex(input);var dock=input.Dock;var anchor=input.Anchor;var bounds=input.Bounds;var margin=input.Margin;int tab=input.TabIndex;var table=parent as TableLayoutPanel;TableLayoutPanelCellPosition cell=table==null?new TableLayoutPanelCellPosition():table.GetCellPosition(input);int cols=table==null?1:table.GetColumnSpan(input),rows=table==null?1:table.GetRowSpan(input);parent.Controls.Remove(input);var frame=new InputFrame(input){Bounds=bounds,Margin=margin,Dock=dock,Anchor=anchor,TabIndex=tab};if(!(input is TextBox&&((TextBox)input).Multiline)&&dock!=DockStyle.Fill)frame.Height=42;
        if(table==null){parent.Controls.Add(frame);parent.Controls.SetChildIndex(frame,index);}else{table.Controls.Add(frame,cell.Column,cell.Row);table.SetColumnSpan(frame,cols);table.SetRowSpan(frame,rows);}frame.Arrange();}
}
class HarborChoice:ComboBox
{
    public HarborChoice(){FlatStyle=FlatStyle.Flat;DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=27;DropDownStyle=ComboBoxStyle.DropDownList;}
    protected override void OnDrawItem(DrawItemEventArgs e){if(e.Index<0)return;Color bg=(e.State&DrawItemState.Selected)!=0?Color.FromArgb(35,72,75):BackColor;using(var brush=new SolidBrush(bg))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-12,e.Bounds.Height),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
    void PaintChoice(Graphics g){using(var fill=new SolidBrush(BackColor))using(var pen=new Pen(Enabled?ForeColor:Color.Gray,1.5f)){g.FillRectangle(fill,ClientRectangle);TextRenderer.DrawText(g,Text,Font,new Rectangle(8,0,Math.Max(10,Width-34),Height),Enabled?ForeColor:Color.Gray,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);int x=Width-15,y=Height/2;g.SmoothingMode=SmoothingMode.AntiAlias;g.DrawLines(pen,new[]{new Point(x-4,y-2),new Point(x,y+2),new Point(x+4,y-2)});}}
    protected override void WndProc(ref Message m){base.WndProc(ref m);if(!IsHandleCreated)return;if(m.Msg==0xF||m.Msg==0x85){using(var g=Graphics.FromHwnd(Handle))PaintChoice(g);}else if((m.Msg==0x317||m.Msg==0x318)&&m.WParam!=IntPtr.Zero){using(var g=Graphics.FromHdc(m.WParam))PaintChoice(g);}}
}
class HarborProgress:Control
{
    int value;
    ProgressBarStyle style;
    public int Minimum=0,Maximum=100;
    public int Value{get{return value;}set{this.value=Math.Max(Minimum,Math.Min(Maximum,value));Invalidate();AccessibilityNotifyClients(AccessibleEvents.ValueChange,-1);}}
    public ProgressBarStyle Style{get{return style;}set{style=value;Invalidate();}}
    public HarborProgress(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Color.FromArgb(28,41,54);ForeColor=Color.FromArgb(133,229,206);AccessibleRole=AccessibleRole.ProgressBar;AccessibleName="サーバーパックの進み具合";TabStop=false;}
    protected override AccessibleObject CreateAccessibilityInstance(){return new ProgressAccessibility(this);}
    class ProgressAccessibility:ControlAccessibleObject{readonly HarborProgress progress;public ProgressAccessibility(HarborProgress control):base(control){progress=control;}public override string Value{get{return progress.Style==ProgressBarStyle.Marquee?"処理中":progress.Value+"%";}set{}}}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);int width=Style==ProgressBarStyle.Marquee?Width/3:(int)(Width*(Value-Minimum)/(double)Math.Max(1,Maximum-Minimum));using(var fill=new SolidBrush(ForeColor))e.Graphics.FillRectangle(fill,Style==ProgressBarStyle.Marquee?Width/3:0,0,width,Height);}
}
class HarborNumber:NumericUpDown
{
    public HarborNumber(){BorderStyle=BorderStyle.None;foreach(Control child in Controls)if(child.GetType().Name.Contains("UpDownButtons")){var buttons=child;buttons.Paint+=delegate(object sender,PaintEventArgs e){e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var pen=new Pen(Enabled?ForeColor:Color.Gray,1.4f)){int x=buttons.Width/2,y=buttons.Height/4;e.Graphics.DrawLines(pen,new[]{new Point(x-3,y+1),new Point(x,y-2),new Point(x+3,y+1)});y=buttons.Height*3/4;e.Graphics.DrawLines(pen,new[]{new Point(x-3,y-1),new Point(x,y+2),new Point(x+3,y-1)});}};}}
}
class HarborCheckBox:CheckBox
{
    public HarborCheckBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Padding=new Padding(0,3,0,3);Cursor=Cursors.Hand;}
    public override Size GetPreferredSize(Size proposed){Size text=TextRenderer.MeasureText(Text,Font);return new Size(text.Width+32,Math.Max(30,text.Height+10));}
    protected override void OnPaint(PaintEventArgs e){Color accent=BackColor==Color.Black?Color.White:Color.FromArgb(133,229,206);Color surface=BackColor==Color.Black?Color.Black:Color.FromArgb(22,35,47);e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var r=new RectangleF(1,(Height-18)/2,18,18);using(var path=SurfacePaint.Round(r,4))using(var fill=new SolidBrush(Checked?accent:surface))using(var pen=new Pen(Checked?accent:Color.FromArgb(112,140,157),1.5f)){e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}if(Checked)using(var p=new Pen(BackColor==Color.Black?Color.Black:Color.FromArgb(16,25,35),2)){p.StartCap=p.EndCap=LineCap.Round;e.Graphics.DrawLines(p,new[]{new PointF(5,r.Y+9),new PointF(9,r.Y+13),new PointF(16,r.Y+5)});}TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(29,0,Math.Max(0,Width-29),Height),Enabled?ForeColor:Color.Gray,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);if(Focused)using(var p=new Pen(Color.FromArgb(241,215,120)))e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1);}
}
