using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ColorRamp;

internal static class ScreenPicker
{
    public static Bitmap Capture(Rectangle bounds)
    {
        try
        {
            var capture = DesktopCapture.Capture(bounds);
            if (!IsBlack(capture)) return capture;
            // Some drivers return a successful but empty duplication surface.
            // Check a second capture before replacing a visible desktop with it.
            using var fallback = CaptureGdi(bounds);
            if (IsBlack(fallback)) return capture; // A genuinely black desktop is valid.
            capture.Dispose();
            return new Bitmap(fallback);
        }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"DirectX capture unavailable: {ex.Message}"); }
        return CaptureGdi(bounds);
    }
    internal static bool IsBlack(Bitmap bitmap)
    {
        for (int y=0; y<bitmap.Height; y+=Math.Max(1,bitmap.Height/64))
        for (int x=0; x<bitmap.Width; x+=Math.Max(1,bitmap.Width/64))
        {
            var pixel=bitmap.GetPixel(x,y);
            if(pixel.R!=0 || pixel.G!=0 || pixel.B!=0) return false;
        }
        return true;
    }
    static Bitmap CaptureGdi(Rectangle bounds)
    {
        var bitmap=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format24bppRgb);
        try { using var graphics=Graphics.FromImage(bitmap); graphics.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size,CopyPixelOperation.SourceCopy); return bitmap; }
        catch { bitmap.Dispose(); throw; }
    }
    public static Task<Color?> PickAsync()
    {
        var completion=new TaskCompletionSource<Color?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var overlays=new List<PickerOverlay>();
        bool finished=false;
        void Finish(Color? color)
        {
            if(finished) return; finished=true;
            foreach(var overlay in overlays) { overlay.Close(); overlay.Dispose(); }
            completion.TrySetResult(color);
        }
        try
        {
            // Capture every display before showing any overlay, so overlays never contaminate sampling.
            var screens=Screen.AllScreens;
            for(int i=0;i<screens.Length;i++) overlays.Add(new PickerOverlay(screens[i].Bounds,Capture(screens[i].Bounds),i+1,Finish));
            foreach(var overlay in overlays) overlay.Show();
            overlays.FirstOrDefault(o=>o.DisplayBounds.Contains(Cursor.Position))?.Activate();
        }
        catch { foreach(var overlay in overlays) overlay.Dispose(); throw; }
        return completion.Task;
    }
}

internal sealed class PickerOverlay : Form
{
    readonly Bitmap screenshot;
    readonly int display;
    readonly Action<Color?> finish;
    readonly System.Windows.Forms.Timer timer=new() { Interval=30 };
    readonly Font labelFont=new("Segoe UI",10);
    readonly Font smallFont=new("Segoe UI",8);
    Point pointer;
    Rectangle previousCard;
    public Rectangle DisplayBounds { get; }
    [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    public PickerOverlay(Rectangle bounds,Bitmap screenshot,int display,Action<Color?> finish)
    {
        this.screenshot=screenshot; this.display=display; this.finish=finish; DisplayBounds=bounds;
        AutoScaleMode=AutoScaleMode.None;
        FormBorderStyle=FormBorderStyle.None;
        StartPosition=FormStartPosition.Manual;
        Bounds=bounds;
        ShowInTaskbar=false; TopMost=true; KeyPreview=true; DoubleBuffered=true; Cursor=Cursors.Cross;
        Text=$"Color Ramp screen picker · Display {display}";
        Shown+=(_,_) => { SetWindowPos(Handle,new IntPtr(-1),bounds.X,bounds.Y,bounds.Width,bounds.Height,0x0040); UpdatePointer(); };
        timer.Tick+=(_,_) => UpdatePointer(); timer.Start();
        MouseDown+=(_,e) => { if(e.Button==MouseButtons.Left) { var p=PointToScreen(e.Location); finish(Sample(p)); } else if(e.Button==MouseButtons.Right) finish(null); };
        KeyDown+=(_,e) => {
            if(e.KeyCode==Keys.Escape) { finish(null); e.Handled=true; }
            else if(e.KeyCode==Keys.Enter) { finish(Sample(System.Windows.Forms.Cursor.Position)); e.Handled=true; }
            else if(e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down)
            {
                var p=System.Windows.Forms.Cursor.Position;
                p.Offset(e.KeyCode==Keys.Left?-1:e.KeyCode==Keys.Right?1:0,e.KeyCode==Keys.Up?-1:e.KeyCode==Keys.Down?1:0);
                System.Windows.Forms.Cursor.Position=p; e.Handled=true;
            }
        };
        FormClosing+=(_,_) => timer.Stop();
    }
    Color Sample(Point global)
    {
        var p=new Point(global.X-DisplayBounds.X,global.Y-DisplayBounds.Y);
        return screenshot.GetPixel(Math.Clamp(p.X,0,screenshot.Width-1),Math.Clamp(p.Y,0,screenshot.Height-1));
    }
    internal void CancelForTest() => finish(null);
    Rectangle Card(Point p)
    {
        int w=184,h=242;
        int x=p.X+28,y=p.Y+24;
        if(x+w>ClientSize.Width) x=p.X-w-28;
        if(y+h>ClientSize.Height) y=p.Y-h-24;
        return new Rectangle(Math.Clamp(x,0,Math.Max(0,ClientSize.Width-w)),Math.Clamp(y,0,Math.Max(0,ClientSize.Height-h)),w,h);
    }
    void UpdatePointer()
    {
        var global=System.Windows.Forms.Cursor.Position;
        var p=new Point(global.X-DisplayBounds.X,global.Y-DisplayBounds.Y);
        if(p==pointer) return;
        Invalidate(previousCard);
        pointer=p;
        if(DisplayBounds.Contains(global)) { previousCard=Card(p); Invalidate(previousCard); }
        else previousCard=Rectangle.Empty;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImageUnscaled(screenshot,0,0);
        var global=System.Windows.Forms.Cursor.Position;
        if(!DisplayBounds.Contains(global)) return;
        var p=new Point(global.X-DisplayBounds.X,global.Y-DisplayBounds.Y);
        var card=Card(p); var g=e.Graphics;
        using(var background=new SolidBrush(Color.FromArgb(34,36,40))) g.FillRectangle(background,card);
        using(var border=new Pen(Color.FromArgb(90,94,101))) g.DrawRectangle(border,card);
        int startX=card.X+15,startY=card.Y+14,cell=14;
        for(int y=-5;y<=5;y++) for(int x=-5;x<=5;x++)
        {
            var c=screenshot.GetPixel(Math.Clamp(p.X+x,0,screenshot.Width-1),Math.Clamp(p.Y+y,0,screenshot.Height-1));
            using var brush=new SolidBrush(c);
            g.FillRectangle(brush,startX+(x+5)*cell,startY+(y+5)*cell,cell,cell);
            using var grid=new Pen(Color.FromArgb(35,255,255,255));
            g.DrawRectangle(grid,startX+(x+5)*cell,startY+(y+5)*cell,cell,cell);
        }
        using(var outline=new Pen(Color.Black,3)) g.DrawRectangle(outline,startX+5*cell,startY+5*cell,cell,cell);
        using(var outline=new Pen(Color.White,1)) g.DrawRectangle(outline,startX+5*cell,startY+5*cell,cell,cell);
        var color=Sample(global);
        using(var brush=new SolidBrush(color)) g.FillRectangle(brush,card.X+15,card.Y+181,36,23);
        TextRenderer.DrawText(g,RampDocument.ToHex(color),labelFont,new Point(card.X+60,card.Y+181),Color.White);
        TextRenderer.DrawText(g,$"Display {display} · {global.X}, {global.Y}",smallFont,new Point(card.X+12,card.Y+209),Color.Silver);
        TextRenderer.DrawText(g,"Click to apply · Esc to cancel",smallFont,new Point(card.X+12,card.Y+225),Color.Silver);
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing) { timer.Dispose(); screenshot.Dispose(); labelFont.Dispose(); smallFont.Dispose(); }
        base.Dispose(disposing);
    }
}
