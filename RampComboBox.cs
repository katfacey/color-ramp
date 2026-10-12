using System.Drawing.Drawing2D;

namespace ColorRamp;

// Keep native selection, keyboard navigation and accessibility with a custom face.
internal sealed class RampComboBox : ComboBox
{
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != 0x000F || !IsHandleCreated) return;
        using var g = Graphics.FromHwnd(Handle);
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 5);
        using var fill = new SolidBrush(BackColor);
        using var border = new Pen(Color.FromArgb(65, ForeColor));
        g.FillPath(fill, shape); g.DrawPath(border, shape);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(10, 0, Math.Max(0, Width - 38), Height), ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        using var chevron = new Pen(ForeColor, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float x = Width - 17, y = Height / 2f;
        g.DrawLines(chevron, [new PointF(x - 4, y - 2), new PointF(x, y + 2), new PointF(x + 4, y - 2)]);
    }
    static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var path = new GraphicsPath(); float d = radius * 2;
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var shape = Rounded(new RectangleF(0, 0, Width, Height), 5);
        var old = Region; Region = new Region(shape); old?.Dispose();
    }
}
