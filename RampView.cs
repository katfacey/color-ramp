using System.Drawing.Drawing2D;

namespace ColorRamp;

internal sealed class RampView : Control
{
    public RampDocument Document { get; set; } = new();
    public RampStop? Selected { get; set; }
    public bool Dark { get; set; }
    public event Action<RampStop>? SelectionChanged;
    public event Action? BeforeDrag;
    public event Action? StopMoved;
    public event Action? EditColor;
    bool dragging, remembered;
    public RampView() { DoubleBuffered=true; TabStop=true; Cursor=Cursors.Hand; AccessibleName="Color ramp; drag stops or double-click to edit"; }
    int LeftEdge => 10;
    int RightEdge => Width-11;
    int X(RampStop s) => LeftEdge+(int)Math.Round(s.Position*(RightEdge-LeftEdge));
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g=e.Graphics;
        var area=new Rectangle(LeftEdge,8,RightEdge-LeftEdge+1,52);
        using(var checkerLight=new SolidBrush(Color.FromArgb(195,195,195)))
        using(var checkerDark=new SolidBrush(Color.FromArgb(150,150,150)))
        {
            for(int x=area.Left;x<area.Right;x+=8) for(int y=area.Top;y<area.Bottom;y+=8)
                g.FillRectangle(((x-area.Left)/8+(y-area.Top)/8)%2==0?checkerLight:checkerDark,x,y,Math.Min(8,area.Right-x),Math.Min(8,area.Bottom-y));
        }
        for(int x=area.Left;x<area.Right;x++) { using var pen=new Pen(Document.Sample((x-area.Left)/(double)(area.Width-1))); g.DrawLine(pen,x,area.Top,x,area.Bottom-1); }
        using(var border=new Pen(Dark?Color.FromArgb(90,93,98):Color.FromArgb(169,174,181))) g.DrawRectangle(border,area);
        g.SmoothingMode=SmoothingMode.AntiAlias;
        foreach(var stop in Document.Stops.OrderBy(s => s==Selected ? 1:0))
        {
            int x=X(stop);
            Point[] shape=[new(x,53),new(x+8,63),new(x+8,78),new(x-8,78),new(x-8,63)];
            using var fill=new SolidBrush(stop.Color);
            using var outline=new Pen(stop==Selected?Color.White:Color.FromArgb(25,28,33),stop==Selected?3:1.5f);
            g.FillPolygon(fill,shape); g.DrawPolygon(outline,shape);
            if(stop==Selected) { using var pen=new Pen(Color.FromArgb(34,37,41)); g.DrawPolygon(pen,shape); }
        }
        TextRenderer.DrawText(g,"0.0",Font,new Point(LeftEdge,80),Dark?Color.FromArgb(158,164,171):Color.FromArgb(111,118,129));
        TextRenderer.DrawText(g,"1.0",Font,new Rectangle(RightEdge-30,80,34,20),Dark?Color.FromArgb(158,164,171):Color.FromArgb(111,118,129),TextFormatFlags.Right);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); if(e.Button!=MouseButtons.Left) return;
        Focus();
        var nearby=Document.Stops.OrderBy(s=>Math.Abs(X(s)-e.X)).First();
        if(Math.Abs(X(nearby)-e.X)>14 || e.Y<46 || e.Y>82) return;
        Selected=nearby; SelectionChanged?.Invoke(nearby); dragging=true; remembered=false; Capture=true; Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if(!dragging || Selected is null) return;
        var p=Math.Clamp((e.X-LeftEdge)/(double)(RightEdge-LeftEdge),0,1);
        if(Math.Abs(p-Selected.Position)<.00001) return;
        if(!remembered) { BeforeDrag?.Invoke(); remembered=true; }
        Selected.Position=p;
        Document.Stops=Document.Stops.OrderBy(s=>s.Position).ToList();
        StopMoved?.Invoke(); Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging=false; Capture=false; }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if(!Capture) dragging=false; }
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        var nearby=Document.Stops.OrderBy(s=>Math.Abs(X(s)-e.X)).First();
        if(Math.Abs(X(nearby)-e.X)<=14 && e.Y>=46 && e.Y<=82) { Selected=nearby; SelectionChanged?.Invoke(nearby); EditColor?.Invoke(); }
    }
}
