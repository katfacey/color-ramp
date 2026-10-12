using System.Globalization;

namespace ColorRamp;

// Compact Blender-style numeric slider: drag to adjust, double-click to type.
internal sealed class ValueSlider : Control, IMessageFilter
{
    decimal value, maximum = 1;
    TextBox? editor;
    int dragStartX, wheelRemainder;
    decimal dragStartValue;
    public decimal Minimum { get; set; }
    public bool StepSelector { get; set; }
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
        Application.AddMessageFilter(this);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor??BackColor);
        e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var shape=RoundedUi.Path(new RectangleF(.5f,.5f,Width-1,Height-1),LogicalToDeviceUnits(5));
        using(var background=new SolidBrush(BackColor))e.Graphics.FillPath(background,shape);
        float fraction = Maximum == Minimum ? 0 : (float)((Value - Minimum) / (Maximum - Minimum));
        using var fill = new SolidBrush(Color.FromArgb(85, SystemColors.Highlight));
        if(!StepSelector){var state=e.Graphics.Save();e.Graphics.SetClip(shape);e.Graphics.FillRectangle(fill,0,0,fraction*Width,Height);e.Graphics.Restore(state);}
        using var border = new Pen(Focused ? SystemColors.Highlight : Color.FromArgb(128, ForeColor));
        e.Graphics.DrawPath(border,shape);
        if(StepSelector){using var pen=new Pen(ForeColor,1.5f);float y=Height/2f;e.Graphics.DrawLines(pen,[new PointF(14,y-4),new PointF(10,y),new PointF(14,y+4)]);e.Graphics.DrawLines(pen,[new PointF(Width-14,y-4),new PointF(Width-10,y),new PointF(Width-14,y+4)]);}
        TextRenderer.DrawText(e.Graphics, Value.ToString($"F{DecimalPlaces}", CultureInfo.CurrentCulture), Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
    internal decimal DragValue(decimal initial,int distance) => initial + (Maximum-Minimum)*distance/Math.Max(1,LogicalToDeviceUnits(200));
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button != MouseButtons.Left || e.Clicks > 1) return; Focus(); if(StepSelector){if(e.X<Width/3)Value-=1;else if(e.X>Width*2/3)Value+=1;return;} dragStartX=e.X;dragStartValue=Value;Capture = true; }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (!StepSelector && Capture && e.Button == MouseButtons.Left) Value=DragValue(dragStartValue,e.X-dragStartX); }
    protected override void OnMouseUp(MouseEventArgs e) { Capture = false; base.OnMouseUp(e); }
    protected override void OnMouseDoubleClick(MouseEventArgs e) { base.OnMouseDoubleClick(e); BeginEdit(); }
    internal void ScrollValue(int delta)
    {
        if(StepSelector)return;
        wheelRemainder+=delta;int steps=wheelRemainder/120;wheelRemainder%=120;
        if(steps!=0){EndEdit(true);Value+=steps*Increment;}
    }
    protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);ScrollValue(e.Delta);}
    public bool PreFilterMessage(ref Message message)
    {
        if(message.Msg!=0x020A||StepSelector||!Visible||!IsHandleCreated||FindForm()!=Form.ActiveForm||!RectangleToScreen(ClientRectangle).Contains(System.Windows.Forms.Cursor.Position))return false;
        if(Control.FromHandle(message.HWnd) is ToolStripDropDown)return false;
        ScrollValue((short)((long)message.WParam>>16));return true;
    }
    protected override void Dispose(bool disposing){if(disposing)Application.RemoveMessageFilter(this);base.Dispose(disposing);}
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
        editor = new TextBox { Text = Value.ToString($"F{DecimalPlaces}"), Bounds = new Rectangle(6, Math.Max(2,(Height-Font.Height-4)/2),Width-12,Font.Height+4), BorderStyle=BorderStyle.None, Font = Font, BackColor = BackColor, ForeColor = ForeColor, TextAlign = HorizontalAlignment.Center };
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
