using System.Drawing.Drawing2D;
namespace ColorRamp;
internal static class RoundedUi
{
    public static GraphicsPath Path(RectangleF r,float radius=5)
    {
        var p=new GraphicsPath(); float d=radius*2;
        p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
}
internal sealed class RoundedButton:Button
{
    bool hover;
    public RoundedButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.Clear(Parent?.BackColor??BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
        using var path=RoundedUi.Path(new RectangleF(.5f,.5f,Width-1,Height-1),LogicalToDeviceUnits(5));
        using var fill=new SolidBrush(hover?ControlPaint.Light(BackColor,.12f):BackColor);using var border=new Pen(Focused?SystemColors.Highlight:FlatAppearance.BorderColor);
        g.FillPath(fill,path);g.DrawPath(border,path);
        int imageWidth=Image?.Width??0;
        if(Image!=null)g.DrawImage(Image,string.IsNullOrEmpty(Text)?(Width-imageWidth)/2:10,(Height-Image.Height)/2);
        var bounds=new Rectangle(Image!=null&&!string.IsNullOrEmpty(Text)?imageWidth+16:4,0,Width-(Image!=null&&!string.IsNullOrEmpty(Text)?imageWidth+20:8),Height);
        TextRenderer.DrawText(g,Text,Font,bounds,Enabled?ForeColor:SystemColors.GrayText,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(AccessibleName=="Ramp tools"){using var pen=new Pen(ForeColor,1.6f);float x=Width/2f,y=Height/2f;g.DrawLines(pen,[new PointF(x-5,y-2),new PointF(x,y+3),new PointF(x+5,y-2)]);}
    }
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Invalidate();}
}
internal sealed class RoundedTextBox:UserControl
{
    readonly TextBox input=new(){BorderStyle=BorderStyle.None};
    public int MaxLength{get=>input.MaxLength;set=>input.MaxLength=value;}
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text{get=>input.Text;set{input.Text=value??"";}}
    public RoundedTextBox()
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
        Controls.Add(input);input.TextChanged+=(_,_)=>OnTextChanged(EventArgs.Empty);input.Leave+=(_,_)=>OnLeave(EventArgs.Empty);input.KeyDown+=(_,e)=>OnKeyDown(e);
        input.Enter+=(_,_)=>Invalidate(); input.Leave+=(_,_)=>Invalidate();
    }
    protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);input.Bounds=new Rectangle(8,Math.Max(3,(Height-input.PreferredHeight)/2),Math.Max(1,Width-16),input.PreferredHeight);}
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);if(input!=null)input.Font=Font;}
    protected override void OnBackColorChanged(EventArgs e){base.OnBackColorChanged(e);if(input!=null)input.BackColor=BackColor;Invalidate();}
    protected override void OnForeColorChanged(EventArgs e){base.OnForeColorChanged(e);if(input!=null)input.ForeColor=ForeColor;Invalidate();}
    protected override void OnEnter(EventArgs e){base.OnEnter(e);input.Focus();}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.Clear(Parent?.BackColor??BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
        using var path=RoundedUi.Path(new RectangleF(.5f,.5f,Width-1,Height-1),LogicalToDeviceUnits(5));using var fill=new SolidBrush(BackColor);using var border=new Pen(ContainsFocus?SystemColors.Highlight:Color.FromArgb(100,ForeColor));g.FillPath(fill,path);g.DrawPath(border,path);
    }
}
