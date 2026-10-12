namespace ColorRamp;

internal static class SelfTest
{
    public static string Run()
    {
        int checks=0;
        void Check(bool condition,string name) { if(!condition) throw new Exception("FAILED: "+name); checks++; }
        var ramp=new RampDocument { Stops=[new() { Position=0,Hex="#000000",Alpha=0 }, new() { Position=1,Hex="#FFFFFF",Alpha=1 }] };
        ramp.Validate();
        Check(ramp.Sample(0).A==0 && ramp.Sample(1).R==255,"endpoint color and alpha");
        var middle=ramp.Sample(.5);
        Check(middle.R==128 && middle.G==128 && middle.B==128 && middle.A==128,"linear midpoint RGBA");
        ramp.Interpolation="Constant";
        Check(ramp.Sample(.999).R==0 && ramp.Sample(1).R==255,"constant transition");
        ramp.Interpolation="Ease";
        Check(ramp.Sample(.25).R==40,"ease curve");
        foreach(var mode in new[] { "Linear","Cardinal","B-Spline","Ease" }) {
            ramp.Interpolation=mode;
            for(int i=0;i<=100;i++) { var c=ramp.Sample(i/100.0); Check(c.A>=0 && c.A<=255,"spline finite channel"); }
        }
        ramp.Stops=[new() { Position=0,Hex="#FF0000" },new() { Position=1,Hex="#00FF00" }];
        ramp.Interpolation="Linear";
        foreach(var mode in new[] { "HSV","HSL" }) { ramp.ColorMode=mode; var c=ramp.Sample(.5); Check(c.R==255 && c.G==255 && c.B==0,mode+" hue midpoint"); }
        ramp.HueInterpolation="Far"; var far=ramp.Sample(.5); Check(far.B==255 && far.R==0 && far.G==0,"long hue path");
        ramp.ColorMode="RGB"; ramp.Stops.Insert(1,new() { Position=.5,Hex="#112233" }); ramp.Stops.Insert(2,new() { Position=.5,Hex="#445566" });
        Check(RampDocument.ToHex(ramp.Sample(.5))=="#445566","duplicate stop right continuity");
        Check(RampDocument.TryHex("#Ab12Cd",out var parsed) && RampDocument.ToHex(parsed)=="#AB12CD","HEX parsing");
        Check(!RampDocument.TryHex("bad-input",out _),"invalid HEX rejection");
        var clone=ramp.Clone(); clone.Stops[0].Hex="#000000"; Check(ramp.Stops[0].Hex=="#FF0000","independent undo snapshots");
        var file=Path.Combine(Path.GetTempPath(),"colorramp-test-"+Guid.NewGuid()+".json");
        try { Storage.Write(file,ramp); var restored=Storage.Read(file); Check(restored.Stops.Count==4 && restored.HueInterpolation=="Far","ramp file roundtrip"); }
        finally { if(File.Exists(file)) File.Delete(file); }
        ramp.Stops[0].Position=double.NaN;
        bool rejected=false; try { ramp.Validate(); } catch(InvalidDataException) { rejected=true; }
        Check(rejected,"invalid file positions rejected");
        var displays=Screen.AllScreens;
        foreach(var screen in displays)
        {
            using var capture=ScreenPicker.Capture(screen.Bounds);
            Check(capture.Size==screen.Bounds.Size,"monitor capture size");
            Check(capture.GetPixel(capture.Width/2,capture.Height/2).A==255,"monitor capture readable");
        }
        var picker = ScreenPicker.PickAsync();
        Application.DoEvents();
        var overlays = Application.OpenForms.OfType<PickerOverlay>().ToArray();
        Check(overlays.Length == displays.Length,"one picker overlay per monitor");
        foreach(var overlay in overlays)
        {
            Check(overlay.Bounds == overlay.DisplayBounds,"picker physical monitor bounds");
            using var rendered = new Bitmap(overlay.Width,overlay.Height);
            overlay.DrawToBitmap(rendered,new Rectangle(Point.Empty,overlay.Size));
            Check(rendered.Width == overlay.DisplayBounds.Width,"picker rendering");
        }
        overlays[0].CancelForTest();
        Application.DoEvents();
        Check(picker.IsCompleted && picker.Result is null,"picker cancellation completes");
        Check(!Application.OpenForms.OfType<PickerOverlay>().Any(),"picker overlay cleanup");
        using(var main=new MainForm(false))
        {
            main.Show(); Application.DoEvents();
            Check(main.Controls.Cast<Control>().All(c=>main.ClientRectangle.Contains(c.Bounds)),"editor controls within client area");
            main.SetTheme("Light"); main.SetTheme("Dark"); main.Close();
        }
        Exception? lifecycleError = null;
        using(var main = new MainForm(false))
        {
            main.Shown += async (_,_) => {
                try {
                    var pick = main.PickForTest();
                    await Task.Delay(500);
                    var active = Application.OpenForms.OfType<PickerOverlay>().ToArray();
                    Check(active.Length == displays.Length && !main.Visible,"main editor hides while picking");
                    active[0].CancelForTest();
                    await pick;
                    Check(main.Visible && !main.IsDisposed,"editor returns after cancellation");
                } catch(Exception ex) { lifecycleError = ex; }
                finally { main.Close(); }
            };
            Application.Run(main);
        }
        if(lifecycleError is not null) throw lifecycleError;
        return $"PASS: {checks} checks. Interpolation, alpha, hue paths, duplicate stops, validation, file roundtrip, editor layout and {displays.Length} monitor capture(s).";
    }
}
