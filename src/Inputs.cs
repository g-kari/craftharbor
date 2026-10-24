using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

class HarborDialog:Form
{
    public HarborDialog(){AutoScaleMode=AutoScaleMode.Dpi;}
    static bool HasOpenChoice(Control container)
    {
        foreach(Control child in container.Controls){var choice=child as ComboBox;if(choice!=null&&choice.DroppedDown||HasOpenChoice(child))return true;}return false;
    }
    bool HandleEscape(Keys keyData)
    {
        // Import dialogs have their own cancel button, which also cancels active downloads.
        // An open choice keeps its native Escape handling instead of closing the whole form.
        if(keyData!=Keys.Escape||CancelButton!=null||HasOpenChoice(this))return false;
        Close();return true;
    }
    protected override bool ProcessCmdKey(ref Message message,Keys keyData){return HandleEscape(keyData)||base.ProcessCmdKey(ref message,keyData);}
    protected override bool ProcessDialogKey(Keys keyData){return HandleEscape(keyData)||base.ProcessDialogKey(keyData);}
}

class InputFrame:Panel
{
    readonly Control input;
    bool hover;
    public InputFrame(Control control){input=control;DoubleBuffered=true;BackColor=HarborTheme.Input;ForeColor=control.ForeColor;Name="frame-"+control.Name;AccessibleRole=AccessibleRole.Grouping;AccessibleName=control.AccessibleName;TabStop=false;
        control.Enter+=delegate{Invalidate();};control.Leave+=delegate{Invalidate();};control.MouseEnter+=delegate{hover=true;Invalidate();};control.MouseLeave+=delegate{hover=false;Invalidate();};control.EnabledChanged+=delegate{Invalidate();};
        var text=control as TextBox;if(text!=null)text.BorderStyle=BorderStyle.None;var number=control as NumericUpDown;if(number!=null)number.BorderStyle=BorderStyle.None;control.BackColor=BackColor;Controls.Add(control);Resize+=delegate{Arrange();};control.FontChanged+=delegate{FitHeight();Arrange();};FontChanged+=delegate{FitHeight();Arrange();};Arrange();}
    void FitHeight(){if(input!=null&&Dock!=DockStyle.Fill&&!(input is TextBox&&((TextBox)input).Multiline))Height=Math.Max(48,input.PreferredSize.Height+18);}
    void Arrange(){if(input==null)return;bool multiline=input is TextBox&&((TextBox)input).Multiline;input.Dock=DockStyle.None;int inset=multiline?12:12;input.SetBounds(inset,multiline?10:Math.Max(7,(Height-input.PreferredSize.Height)/2),Math.Max(30,Width-inset*2),multiline?Math.Max(20,Height-20):input.PreferredSize.Height);}
    protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;bool contrast=BackColor==Color.Black;Color border=contrast?Color.White:input.ContainsFocus?HarborTheme.Live:hover?Color.FromArgb(107,137,153):HarborTheme.Border;using(var path=SurfacePaint.Round(new RectangleF(1,1,Width-3,Height-3),3))using(var fill=new SolidBrush(BackColor))using(var pen=new Pen(border,input.ContainsFocus?2:1)){e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}}
    public static void Wrap(Control input){if(input.Parent==null||input.Parent is InputFrame)return;var parent=input.Parent;int index=parent.Controls.GetChildIndex(input);var dock=input.Dock;var anchor=input.Anchor;var bounds=input.Bounds;var margin=input.Margin;int tab=input.TabIndex;var table=parent as TableLayoutPanel;TableLayoutPanelCellPosition cell=table==null?new TableLayoutPanelCellPosition():table.GetCellPosition(input);int cols=table==null?1:table.GetColumnSpan(input),rows=table==null?1:table.GetRowSpan(input);parent.Controls.Remove(input);var frame=new InputFrame(input){Bounds=bounds,Margin=margin,Dock=dock,Anchor=anchor,TabIndex=tab};if(!(input is TextBox&&((TextBox)input).Multiline)&&dock!=DockStyle.Fill)frame.Height=42;
        if(table==null){parent.Controls.Add(frame);parent.Controls.SetChildIndex(frame,index);}else{table.Controls.Add(frame,cell.Column,cell.Row);table.SetColumnSpan(frame,cols);table.SetRowSpan(frame,rows);}frame.FitHeight();frame.Arrange();}
}
class HarborChoice:ComboBox
{
    public HarborChoice(){FlatStyle=FlatStyle.Flat;DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=27;DropDownStyle=ComboBoxStyle.DropDownList;}
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);ItemHeight=Math.Max(27,Font.Height+4);}
    protected override void OnDrawItem(DrawItemEventArgs e){if(e.Index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0;bool contrast=HarborTheme.Contrast(this);Color bg=selected?(contrast?Color.White:HarborTheme.Primary):BackColor;Color fg=selected&&contrast?Color.Black:ForeColor;using(var brush=new SolidBrush(bg))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-12,e.Bounds.Height),fg,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
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
    public HarborProgress(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);BackColor=HarborTheme.Panel;ForeColor=HarborTheme.Live;AccessibleRole=AccessibleRole.ProgressBar;AccessibleName="サーバーパックの進み具合";TabStop=false;}
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
    public override Size GetPreferredSize(Size proposed){int width=MaximumSize.Width>32?MaximumSize.Width-32:32767;Size text=TextRenderer.MeasureText(Text,Font,new Size(width,0),TextFormatFlags.WordBreak);return new Size(text.Width+32,Math.Max(30,text.Height+10));}
    protected override void OnPaint(PaintEventArgs e){Color accent=BackColor==Color.Black?Color.White:HarborTheme.Live;Color surface=BackColor==Color.Black?Color.Black:HarborTheme.Input;e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var r=new RectangleF(1,(Height-18)/2,18,18);using(var path=SurfacePaint.Round(r,2))using(var fill=new SolidBrush(Checked?accent:surface))using(var pen=new Pen(Checked?accent:Color.FromArgb(112,140,157),1.5f)){e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}if(Checked)using(var p=new Pen(BackColor==Color.Black?Color.Black:HarborTheme.Background,2)){p.StartCap=p.EndCap=LineCap.Round;e.Graphics.DrawLines(p,new[]{new PointF(5,r.Y+9),new PointF(9,r.Y+13),new PointF(16,r.Y+5)});}TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(29,0,Math.Max(0,Width-29),Height),Enabled?ForeColor:Color.Gray,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);if(Focused)using(var p=new Pen(HarborTheme.Focus))e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1);}
}

/// <summary>Native numeric fields keep time editing legible in dark/high-contrast themes.</summary>
class HarborTimeField:UserControl
{
    readonly HarborNumber hour,minute;
    DateTime day=DateTime.Today;
    public DateTime Value {get{return day.AddHours((double)hour.Value).AddMinutes((double)minute.Value);}set{day=value.Date;hour.Value=value.Hour;minute.Value=value.Minute;}}
    public HarborTimeField()
    {
        Size=new Size(218,52);TabStop=false;AccessibleName="毎日の再起動時刻";
        var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,26));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        hour=new HarborNumber{Minimum=0,Maximum=23,Dock=DockStyle.Fill,BackColor=HarborTheme.Input,ForeColor=HarborTheme.Text,AccessibleName="再起動時刻・時（0〜23）",Margin=Padding.Empty};
        minute=new HarborNumber{Minimum=0,Maximum=59,Dock=DockStyle.Fill,BackColor=HarborTheme.Input,ForeColor=HarborTheme.Text,AccessibleName="再起動時刻・分（0〜59）",Margin=Padding.Empty};
        row.Controls.Add(hour,0,0);row.Controls.Add(new Label{Text=":",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Margin=Padding.Empty},1,0);row.Controls.Add(minute,2,0);Controls.Add(row);
        FontChanged+=delegate{Height=Math.Max(52,Font.Height+28);};
    }
}
