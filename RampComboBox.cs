using System.Drawing.Drawing2D;
namespace ColorRamp;
internal sealed class RampComboBox : Control
{
    public List<string> Items { get; } = new();
    string? selected;
    bool hovering;
    ContextMenuStrip? menu;
    public string? SelectedItem { get => selected; set { if(selected==value) return; selected=value; Text=value??""; Invalidate(); SelectedIndexChanged?.Invoke(this,EventArgs.Empty); } }
    public event EventHandler? SelectedIndexChanged;
    public RampComboBox() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true); AccessibleRole=AccessibleRole.ComboBox; TabStop=true; Cursor=Cursors.Hand; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics; g.Clear(Parent?.BackColor??BackColor); g.SmoothingMode=SmoothingMode.AntiAlias;
        using var shape=new GraphicsPath(); float d=LogicalToDeviceUnits(10); var r=new RectangleF(.5f,.5f,Width-1,Height-1);
        shape.AddArc(r.Left,r.Top,d,d,180,90); shape.AddArc(r.Right-d,r.Top,d,d,270,90); shape.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); shape.AddArc(r.Left,r.Bottom-d,d,d,90,90); shape.CloseFigure();
        using var fill=new SolidBrush(hovering?ControlPaint.Light(BackColor,.12f):BackColor); using var border=new Pen(Color.FromArgb(Focused?150:65,ForeColor)); g.FillPath(fill,shape); g.DrawPath(border,shape);
        TextRenderer.DrawText(g,Text,Font,new Rectangle(LogicalToDeviceUnits(10),0,Math.Max(0,Width-LogicalToDeviceUnits(38)),Height),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        using var pen=new Pen(ForeColor,1.4f); float x=Width-LogicalToDeviceUnits(17),y=Height/2f,s=LogicalToDeviceUnits(4);
        g.DrawLines(pen,[new PointF(x-s,y-2),new PointF(x,y+2),new PointF(x+s,y-2)]);
    }
    protected override void OnClick(EventArgs e) { base.OnClick(e); Focus(); OpenMenu(); }
    void OpenMenu()
    {
        menu?.Dispose(); menu=new ContextMenuStrip { BackColor=BackColor,ForeColor=ForeColor,Font=Font,ShowImageMargin=false,MinimumSize=new Size(Width,0),Renderer=new ToolStripProfessionalRenderer(new Palette(BackColor,ForeColor)) };
        foreach(var choice in Items) { var item=new ToolStripMenuItem(choice) { Checked=choice==selected }; item.Click+=(_,_)=>{SelectedItem=choice; Focus();}; menu.Items.Add(item); }
        menu.Show(this,new Point(0,Height));
    }
    protected override bool IsInputKey(Keys keyData)=>keyData is Keys.Up or Keys.Down || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(e.KeyCode is Keys.Enter or Keys.Space or Keys.F4 || (e.Alt&&e.KeyCode==Keys.Down)) OpenMenu();
        else if((e.KeyCode is Keys.Up or Keys.Down)&&Items.Count>0) SelectedItem=Items[Math.Clamp(Items.IndexOf(selected??"")+(e.KeyCode==Keys.Up?-1:1),0,Items.Count-1)];
        else return; e.Handled=true; e.SuppressKeyPress=true;
    }
    protected override void OnMouseEnter(EventArgs e) {base.OnMouseEnter(e);hovering=true;Invalidate();}
    protected override void OnMouseLeave(EventArgs e) {base.OnMouseLeave(e);hovering=false;Invalidate();}
    protected override void OnGotFocus(EventArgs e) {base.OnGotFocus(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e) {base.OnLostFocus(e);Invalidate();}
    protected override void Dispose(bool disposing) {if(disposing)menu?.Dispose();base.Dispose(disposing);}
    protected override AccessibleObject CreateAccessibilityInstance()=>new DropdownAccessibility(this);
    sealed class DropdownAccessibility(RampComboBox owner):ControlAccessibleObject(owner)
    {
        public override string? Value { get=>owner.SelectedItem; set {if(value!=null&&owner.Items.Contains(value))owner.SelectedItem=value;} }
        public override string DefaultAction=>"Open";
        public override void DoDefaultAction()=>owner.OpenMenu();
    }
    sealed class Palette(Color background,Color foreground):ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground=>background;
        public override Color MenuBorder=>Color.FromArgb(100,foreground);
        public override Color MenuItemSelected=>ControlPaint.Light(background,.18f);
        public override Color MenuItemBorder=>Color.FromArgb(100,foreground);
    }
}
