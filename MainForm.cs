using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ColorRamp;

internal sealed class MainForm : Form
{
    RampDocument document = new();
    RampStop selected;
    readonly RampView ramp = new();
    readonly NumericUpDown index = new(), position = new(), alpha = new();
    readonly TextBox hex = new();
    readonly ComboBox colorMode = new(), interpolation = new(), hue = new();
    readonly Button swatch = new(), pin = new();
    readonly Label status = new(), count = new();
    readonly ToolTip tips = new();
    readonly Stack<RampDocument> undo = new(), redo = new();
    readonly System.Windows.Forms.Timer themeTimer = new() { Interval = 2000 };
    readonly bool persist;
    bool updating, dark, picking;
    string theme = "System";
    const string SessionFile = "session.json";
    Color surface, text, field;
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public MainForm(bool persist = true)
    {
        this.persist = persist;
        selected = document.Stops[2];
        Text = "Color Ramp";
        Font = new Font("Segoe UI", 10);
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(620, 305);
        MinimumSize = new Size(636, 344);
        MaximumSize = new Size(636, 344);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        var add = AddButton("+", 18, 18, 38, AddStop, "Add a stop between the selected stop and its predecessor");
        AddButton("−", 62, 18, 38, DeleteStop, "Delete selected stop (Delete)");
        AddButton("⋯", 106, 18, 38, ShowMenu, "Ramp tools, export and appearance");
        AddButton("⌖", 150, 18, 40, async () => await Pick(), "Pick a color from any display (E)");
        SetupCombo(colorMode, 204, 18, 100, ["RGB", "HSV", "HSL"]);
        SetupCombo(interpolation, 316, 18, 164, ["Linear", "Constant", "Ease", "Cardinal", "B-Spline"]);
        pin = AddButton("Pin", 492, 18, 110, () => { TopMost = !TopMost; pin.Text = TopMost ? "Pinned ✓" : "Pin"; }, "Keep this window on top");
        ramp.Bounds = new Rectangle(18, 64, 584, 98);
        ramp.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(ramp);
        ramp.Document = document;
        ramp.Selected = selected;
        ramp.SelectionChanged += stop => { selected = stop; RefreshFields(); };
        ramp.BeforeDrag += Remember;
        ramp.StopMoved += () => { RefreshFields(); SaveSession(); };
        ramp.EditColor += EditColor;
        SetupNumber(index, 18, 175, 80, 0, 31, 0, 1);
        AddLabel("Pos", 114, 179, 35);
        SetupNumber(position, 155, 175, 105, 0, 1, 3, .001m);
        SetupCombo(hue, 283, 175, 160, ["Near", "Far", "Clockwise", "Counterclockwise"]);
        count.Bounds = new Rectangle(478, 180, 124, 24);
        count.TextAlign = ContentAlignment.MiddleRight;
        Controls.Add(count);
        swatch = AddButton("", 18, 215, 170, EditColor, "Open color picker; double-click a stop to edit its color");
        AddButton("⌖", 196, 215, 38, async () => await Pick(), "Set this stop from a screen pixel");
        AddLabel("HEX", 246, 220, 38);
        hex.Bounds = new Rectangle(288, 216, 126, 28);
        hex.BorderStyle = BorderStyle.FixedSingle;
        hex.MaxLength = 7;
        Controls.Add(hex);
        AddLabel("Alpha", 434, 220, 47);
        SetupNumber(alpha, 488, 215, 114, 0, 1, 3, .01m);
        AddButton("Pick screen", 18, 261, 140, async () => await Pick(), "Sample any monitor (E); Esc cancels");
        AddButton("Copy color", 170, 261, 130, CopyColor, "Copy selected stop as HEX (Ctrl+C)");
        AddButton("Save ramp", 312, 261, 130, SaveRamp, "Save editable ramp (Ctrl+S)");
        status.Bounds = new Rectangle(454, 266, 148, 25);
        status.TextAlign = ContentAlignment.MiddleRight;
        status.Font = new Font("Segoe UI", 8.5f);
        Controls.Add(status);
        index.ValueChanged += (_, _) => { if (!updating) { selected = document.Stops[(int)index.Value]; RefreshFields(); } };
        position.ValueChanged += (_, _) => { if (!updating) Change(() => { selected.Position = (double)position.Value; Sort(); }); };
        alpha.ValueChanged += (_, _) => { if (!updating) Change(() => selected.Alpha = (double)alpha.Value); };
        hex.Leave += (_, _) => ApplyHex();
        hex.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { ApplyHex(); e.SuppressKeyPress = true; } };
        colorMode.SelectedIndexChanged += (_, _) => { if (!updating) Change(() => document.ColorMode = colorMode.Text); };
        interpolation.SelectedIndexChanged += (_, _) => { if (!updating) Change(() => document.Interpolation = interpolation.Text); };
        hue.SelectedIndexChanged += (_, _) => { if (!updating) Change(() => document.HueInterpolation = hue.Text); };
        if (persist) LoadSession();
        RefreshFields();
        ApplyTheme();
        themeTimer.Tick += (_, _) => { if (theme == "System" && dark != SystemDark()) ApplyTheme(); };
        themeTimer.Start();
        FormClosing += (_, _) => SaveSession();
    }
    Button AddButton(string title, int x, int y, int width, Action action, string tip)
    {
        var button = new Button { Text = title, Bounds = new Rectangle(x, y, width, 32), FlatStyle = FlatStyle.Flat, TabStop = true, UseVisualStyleBackColor = false };
        button.FlatAppearance.BorderSize = 1;
        button.Click += (_, _) => action();
        Controls.Add(button);
        tips.SetToolTip(button, tip);
        button.AccessibleName = string.IsNullOrEmpty(title) ? "Selected stop color" : title;
        return button;
    }
    void AddLabel(string title, int x, int y, int width) => Controls.Add(new Label { Text = title, Bounds = new Rectangle(x,y,width,24) });
    void SetupCombo(ComboBox control, int x, int y, int width, string[] items)
    {
        control.Bounds = new Rectangle(x,y,width,32);
        control.DropDownStyle = ComboBoxStyle.DropDownList;
        control.FlatStyle = FlatStyle.Flat;
        control.DrawMode = DrawMode.OwnerDrawFixed;
        control.ItemHeight = 24;
        control.DrawItem += (_, e) => {
            if(e.Index < 0) return;
            using var brush = new SolidBrush(control.BackColor);
            e.Graphics.FillRectangle(brush,e.Bounds);
            TextRenderer.DrawText(e.Graphics,control.Items[e.Index]?.ToString(),control.Font,e.Bounds,control.ForeColor,TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            if((e.State & DrawItemState.Focus)!=0) e.DrawFocusRectangle();
        };
        control.Items.AddRange(items);
        control.AccessibleName = control == colorMode ? "Color mode" : control == interpolation ? "Interpolation" : "Hue interpolation";
        Controls.Add(control);
    }
    void SetupNumber(NumericUpDown control, int x, int y, int width, decimal min, decimal max, int decimals, decimal increment)
    {
        control.Bounds = new Rectangle(x,y,width,32);
        control.Minimum = min; control.Maximum = max; control.DecimalPlaces = decimals; control.Increment = increment;
        control.BorderStyle = BorderStyle.FixedSingle;
        control.AccessibleName = control == index ? "Selected stop index" : control == position ? "Stop position" : "Stop alpha";
        Controls.Add(control);
    }
    void RefreshFields()
    {
        updating = true;
        index.Maximum = document.Stops.Count-1;
        index.Value = Math.Max(0, document.Stops.IndexOf(selected));
        position.Value = (decimal)selected.Position;
        alpha.Value = (decimal)selected.Alpha;
        hex.Text = selected.Hex.ToUpperInvariant();
        swatch.BackColor = selected.Color;
        colorMode.SelectedItem = document.ColorMode;
        string[] available = document.ColorMode == "RGB" ? ["Linear", "Constant", "Ease", "Cardinal", "B-Spline"] : ["Linear", "Constant", "Ease"];
        if (!interpolation.Items.Cast<string>().SequenceEqual(available))
        {
            interpolation.Items.Clear(); interpolation.Items.AddRange(available);
        }
        interpolation.SelectedItem = document.Interpolation;
        hue.SelectedItem = document.HueInterpolation;
        hue.Visible = document.ColorMode != "RGB";
        // Blender's hue modes use Constant / Linear / Ease, without RGB spline bases.
        if (document.ColorMode != "RGB" && document.Interpolation is "Cardinal" or "B-Spline")
        {
            document.Interpolation = "Linear"; interpolation.SelectedItem = "Linear";
        }
        count.Text = $"{document.Stops.Count} stops";
        ramp.Document = document; ramp.Selected = selected; ramp.Invalidate();
        updating = false;
    }
    void Remember() { undo.Push(document.Clone()); redo.Clear(); }
    void Change(Action action) { Remember(); action(); RefreshFields(); SaveSession(); }
    void Sort() => document.Stops = document.Stops.OrderBy(s => s.Position).ToList();
    void AddStop()
    {
        if (document.Stops.Count >= 32) { status.Text = "32 stop limit"; return; }
        Change(() => {
            var i = document.Stops.IndexOf(selected);
            double p = i > 0 ? (selected.Position + document.Stops[i-1].Position)/2 : (selected.Position + document.Stops[1].Position)/2;
            var c = document.Sample(p);
            selected = new RampStop { Position = p, Hex = RampDocument.ToHex(c), Alpha = c.A/255.0 };
            document.Stops.Add(selected); Sort();
        });
    }
    void DeleteStop()
    {
        if (document.Stops.Count <= 2) { status.Text = "Keep two stops"; return; }
        Change(() => { int i = document.Stops.IndexOf(selected); document.Stops.Remove(selected); selected = document.Stops[Math.Min(i,document.Stops.Count-1)]; });
    }
    void ApplyHex()
    {
        if (!RampDocument.TryHex(hex.Text, out var c)) { hex.Text = selected.Hex; status.Text = "Use 6 HEX digits"; return; }
        if (RampDocument.ToHex(c) != selected.Hex) Change(() => selected.Hex = RampDocument.ToHex(c));
    }
    void EditColor()
    {
        using var dialog = new ColorDialog { Color = selected.Color, FullOpen = true, AnyColor = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) Change(() => selected.Hex = RampDocument.ToHex(dialog.Color));
    }
    internal Task PickForTest() => Pick();
    async Task Pick()
    {
        if (picking) return;
        picking = true;
        Hide();
        try
        {
            await Task.Delay(180); // Let the compositor remove our window before capture.
            Color? color = await ScreenPicker.PickAsync();
            if (color is Color c) Change(() => selected.Hex = RampDocument.ToHex(c));
        }
        catch (Exception ex) { MessageBox.Show($"Could not capture the desktop.\n{ex.Message}", "Color Ramp"); }
        finally { picking = false; Show(); Activate(); status.Text = ""; }
    }
    void CopyColor()
    {
        try { Clipboard.SetText(selected.Hex); status.Text = "Color copied"; }
        catch (ExternalException) { status.Text = "Clipboard busy"; }
    }
    void SaveRamp()
    {
        using var dialog = new SaveFileDialog { Filter = "Color Ramp (*.colorramp.json)|*.colorramp.json", FileName = "Untitled.colorramp.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { Storage.Write(dialog.FileName, document); status.Text = "Ramp saved"; }
        catch (Exception ex) { ShowError(ex); }
    }
    void OpenRamp()
    {
        using var dialog = new OpenFileDialog { Filter = "Color Ramp (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { var loaded = Storage.Read(dialog.FileName); Remember(); document = loaded; selected = document.Stops[0]; RefreshFields(); SaveSession(); status.Text = "Ramp opened"; }
        catch (Exception ex) { ShowError(ex); }
    }
    void ExportPng()
    {
        using var dialog = new SaveFileDialog { Filter = "PNG gradient (*.png)|*.png", FileName = "ramp.png" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try {
            using var bitmap = new Bitmap(1024,64);
            using var g = Graphics.FromImage(bitmap);
            for (int x=0; x<1024; x++) { using var pen=new Pen(document.Sample(x/1023.0)); g.DrawLine(pen,x,0,x,63); }
            bitmap.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png); status.Text = "PNG exported";
        } catch (Exception ex) { ShowError(ex); }
    }
    void ShowMenu()
    {
        var menu = new ContextMenuStrip { BackColor = surface, ForeColor = text };
        void Item(string label, Action action) { var item = menu.Items.Add(label); item.Click += (_,_) => action(); }
        Item("Open ramp…    Ctrl+O", OpenRamp);
        Item("Save ramp…    Ctrl+S", SaveRamp);
        menu.Items.Add(new ToolStripSeparator());
        Item("Flip ramp", () => Change(() => { foreach(var stop in document.Stops) stop.Position = 1-stop.Position; Sort(); }));
        Item("Distribute stops evenly", () => Change(() => { for(int i=0;i<document.Stops.Count;i++) document.Stops[i].Position=i/(double)(document.Stops.Count-1); }));
        Item("Distribute stops from left", () => Change(() => { for(int i=0;i<document.Stops.Count;i++) document.Stops[i].Position=i/(double)document.Stops.Count; }));
        Item("Reset to black and white", () => Change(() => { document = new RampDocument { Stops = [new() { Position=0, Hex="#000000" },new() { Position=1, Hex="#FFFFFF" }] }; selected=document.Stops[0]; }));
        menu.Items.Add(new ToolStripSeparator());
        Item("Undo    Ctrl+Z", () => Undo(false)); Item("Redo    Ctrl+Y", () => Undo(true));
        Item("Export gradient PNG…", ExportPng);
        menu.Items.Add(new ToolStripSeparator());
        foreach(var choice in new[] { "System", "Light", "Dark" })
        {
            var item = new ToolStripMenuItem($"{choice} theme") { Checked = theme == choice };
            item.Click += (_,_) => { SetTheme(choice); SaveSession(); };
            menu.Items.Add(item);
        }
        Item("About / shortcuts", () => MessageBox.Show("Color Ramp 0.1\n\nE: pick screen • Esc: cancel picking\nCtrl+Z / Ctrl+Y: undo / redo\nCtrl+S / Ctrl+O: save / open\nCtrl+C: copy color • Delete: remove stop\nDouble-click a stop: edit color\n\nIndependent app inspired by Blender.\nScreen pixels are sampled as displayed RGB.", "Color Ramp"));
        menu.Closed += (_,_) => menu.Dispose();
        menu.Show(this, new Point(106,52));
    }
    void Undo(bool forward)
    {
        var from = forward ? redo : undo; var to = forward ? undo : redo;
        if (from.Count == 0) return;
        int i = document.Stops.IndexOf(selected);
        to.Push(document.Clone()); document = from.Pop(); selected = document.Stops[Math.Min(i,document.Stops.Count-1)];
        RefreshFields(); SaveSession();
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        bool editing = ActiveControl is TextBox || ActiveControl is NumericUpDown;
        if (keyData == (Keys.Control | Keys.S)) { SaveRamp(); return true; }
        if (keyData == (Keys.Control | Keys.O)) { OpenRamp(); return true; }
        if (keyData == (Keys.Control | Keys.Z) && ActiveControl != hex) { Undo(false); return true; }
        if (keyData == (Keys.Control | Keys.Y) && ActiveControl != hex) { Undo(true); return true; }
        if (!editing && keyData == (Keys.Control | Keys.C)) { CopyColor(); return true; }
        if (!editing && keyData == Keys.Delete) { DeleteStop(); return true; }
        if (!editing && keyData == Keys.E) { _ = Pick(); return true; }
        return base.ProcessCmdKey(ref msg,keyData);
    }
    public void SetTheme(string value) { theme = value; ApplyTheme(); }
    static bool SystemDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }
    void ApplyTheme()
    {
        dark = theme == "Dark" || (theme == "System" && SystemDark());
        surface = dark ? Color.FromArgb(39,41,44) : Color.FromArgb(242,243,245);
        text = dark ? Color.FromArgb(236,237,239) : Color.FromArgb(30,32,35);
        field = dark ? Color.FromArgb(57,60,64) : Color.FromArgb(255,255,255);
        BackColor = surface;
        foreach(Control c in Controls)
        {
            c.ForeColor=text; c.BackColor=c is Label ? surface : field;
            if (c is Button b) { b.FlatAppearance.BorderColor = dark ? Color.FromArgb(83,86,91) : Color.FromArgb(192,195,201); b.FlatAppearance.MouseOverBackColor = dark ? Color.FromArgb(73,77,82) : Color.FromArgb(224,229,235); }
        }
        swatch.BackColor=selected.Color;
        ramp.Dark=dark; ramp.BackColor=surface; ramp.Invalidate();
        int enabled=dark ? 1:0;
        if (IsHandleCreated) DwmSetWindowAttribute(Handle,20,ref enabled,sizeof(int));
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); int enabled=dark?1:0; DwmSetWindowAttribute(Handle,20,ref enabled,sizeof(int)); }
    void SaveSession()
    {
        if (!persist) return;
        try { Directory.CreateDirectory(Storage.DirectoryPath); Storage.Write(Path.Combine(Storage.DirectoryPath,SessionFile),document); File.WriteAllText(Path.Combine(Storage.DirectoryPath,"appearance.json"),JsonSerializer.Serialize(new { Theme=theme, Pinned=TopMost })); }
        catch { status.Text="Autosave unavailable"; }
    }
    void LoadSession()
    {
        try
        {
            var path=Path.Combine(Storage.DirectoryPath,SessionFile);
            if(File.Exists(path)) { document=Storage.Read(path); selected=document.Stops[0]; }
            var settings=Path.Combine(Storage.DirectoryPath,"appearance.json");
            if(File.Exists(settings)) { using var json=JsonDocument.Parse(File.ReadAllText(settings)); var root=json.RootElement; var value=root.GetProperty("Theme").GetString(); if(value is "System" or "Light" or "Dark") theme=value; TopMost=root.GetProperty("Pinned").GetBoolean(); pin.Text=TopMost?"Pinned ✓":"Pin"; }
        }
        catch { status.Text="Default ramp loaded"; }
    }
    void ShowError(Exception ex) => MessageBox.Show(this,ex.Message,"Color Ramp",MessageBoxButtons.OK,MessageBoxIcon.Error);
    protected override void Dispose(bool disposing)
    {
        if(disposing) { themeTimer.Dispose(); tips.Dispose(); }
        base.Dispose(disposing);
    }
}
