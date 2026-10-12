using System.Globalization;

namespace ColorRamp;

// Compact Blender-style numeric slider: drag to adjust, double-click to type.
internal sealed class ValueSlider : Control
{
    decimal value, maximum = 1;
    TextBox? editor;
    public decimal Minimum { get; set; }
    public decimal Maximum { get => maximum; set { maximum = value; Value = this.value; Invalidate(); } }
    public int DecimalPlaces { get; set; }
    public decimal Increment { get; set; } = .001m;
    public decimal Value
    {
        get => value;
        set { var next = Math.Round(Math.Clamp(value, Minimum, Maximum), DecimalPlaces); if (next == this.value) return; this.value = next; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); }
    }
    public event EventHandler? ValueChanged;
    public ValueSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true;
        AccessibleRole = AccessibleRole.Slider;
        Cursor = Cursors.SizeWE;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        float fraction = Maximum == Minimum ? 0 : (float)((Value - Minimum) / (Maximum - Minimum));
        using var fill = new SolidBrush(Color.FromArgb(85, SystemColors.Highlight));
        e.Graphics.FillRectangle(fill, 0, 0, fraction * Width, Height);
        using var border = new Pen(Focused ? SystemColors.Highlight : Color.FromArgb(128, ForeColor));
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        TextRenderer.DrawText(e.Graphics, Value.ToString($"F{DecimalPlaces}", CultureInfo.CurrentCulture), Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
    void SetFromMouse(int x) => Value = Minimum + (Maximum - Minimum) * Math.Clamp((decimal)x / Math.Max(1, Width - 1), 0, 1);
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button != MouseButtons.Left || e.Clicks > 1) return; Focus(); Capture = true; SetFromMouse(e.X); }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (Capture && e.Button == MouseButtons.Left) SetFromMouse(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { Capture = false; base.OnMouseUp(e); }
    protected override void OnMouseDoubleClick(MouseEventArgs e) { base.OnMouseDoubleClick(e); BeginEdit(); }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Left: Value -= Increment; break;
            case Keys.Right: Value += Increment; break;
            case Keys.Home: Value = Minimum; break;
            case Keys.End: Value = Maximum; break;
            case Keys.Enter: case Keys.F2: BeginEdit(); break;
            default: return;
        }
        e.Handled = true; e.SuppressKeyPress = true;
    }
    void BeginEdit()
    {
        if (editor != null) return;
        editor = new TextBox { Text = Value.ToString($"F{DecimalPlaces}"), Bounds = ClientRectangle, Font = Font, BackColor = BackColor, ForeColor = ForeColor, TextAlign = HorizontalAlignment.Center };
        Controls.Add(editor);
        editor.KeyDown += (_, e) => { if (e.KeyCode is Keys.Enter or Keys.Escape) { e.SuppressKeyPress = true; EndEdit(e.KeyCode == Keys.Enter); } };
        editor.Leave += (_, _) => EndEdit(true);
        editor.Focus(); editor.SelectAll();
    }
    void EndEdit(bool commit)
    {
        var input = editor; if (input == null) return; editor = null;
        if (commit && decimal.TryParse(input.Text, out var next)) Value = next;
        Controls.Remove(input); input.Dispose(); Invalidate();
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}
