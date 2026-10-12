using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorRamp;

public sealed class RampStop
{
    public double Position { get; set; }
    public string Hex { get; set; } = "#FFFFFF";
    public double Alpha { get; set; } = 1;
    [JsonIgnore]
    public Color Color => ColorTranslator.FromHtml(Hex);
    public RampStop Clone() => new() { Position = Position, Hex = Hex, Alpha = Alpha };
}

public sealed class RampDocument
{
    public int Version { get; set; } = 1;
    public string ColorMode { get; set; } = "RGB";
    public string Interpolation { get; set; } = "Linear";
    public string HueInterpolation { get; set; } = "Near";
    public List<RampStop> Stops { get; set; } =
    [
        new() { Position = 0, Hex = "#102C46" },
        new() { Position = .35, Hex = "#6BABA7" },
        new() { Position = .65, Hex = "#E58B62" },
        new() { Position = 1, Hex = "#FFF2CD" }
    ];
    public RampDocument Clone() => new() { Version = Version, ColorMode = ColorMode, Interpolation = Interpolation, HueInterpolation = HueInterpolation, Stops = Stops.Select(s => s.Clone()).ToList() };
    public void Validate()
    {
        if (Version != 1 || Stops is null || Stops.Count is < 2 or > 32) throw new InvalidDataException("A ramp needs 2 to 32 stops and file version 1.");
        if (!new[] { "RGB", "HSV", "HSL" }.Contains(ColorMode) || !new[] { "Linear", "Constant", "Ease", "Cardinal", "B-Spline" }.Contains(Interpolation) || !new[] { "Near", "Far", "Clockwise", "Counterclockwise" }.Contains(HueInterpolation)) throw new InvalidDataException("Unrecognized ramp settings.");
        foreach (var stop in Stops)
        {
            if (!double.IsFinite(stop.Position) || stop.Position is < 0 or > 1 || !double.IsFinite(stop.Alpha) || stop.Alpha is < 0 or > 1 || !TryHex(stop.Hex, out _)) throw new InvalidDataException("Invalid stop position, color, or alpha.");
        }
        Stops = Stops.OrderBy(s => s.Position).ToList();
    }
    public static bool TryHex(string? value, out Color color)
    {
        color = Color.Empty;
        var text = value?.Trim().TrimStart('#');
        if (text is null || text.Length != 6 || !int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var rgb)) return false;
        color = Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        return true;
    }
    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    public Color Sample(double position)
    {
        var ordered = Stops.OrderBy(s => s.Position).ToArray();
        int right = Array.FindIndex(ordered, s => s.Position > position);
        bool spline = ColorMode == "RGB" && Interpolation is "Cardinal" or "B-Spline";
        if (right == 0 && !spline) return WithAlpha(ordered[0]);
        if (right < 0 && !spline) return WithAlpha(ordered[^1]);
        if (right < 0) right = ordered.Length;
        var a = ordered[Math.Max(0, right - 1)];
        var b = ordered[Math.Min(ordered.Length - 1, right)];
        // Splines need virtual endpoint stops at 0/1 carrying the nearest stop color.
        // Clamping to a constant outside the real stops creates visible discontinuities.
        double leftPosition = right == 0 ? 0 : a.Position;
        double rightPosition = right == ordered.Length ? 1 : b.Position;
        double t = leftPosition == rightPosition
            ? (right == ordered.Length ? 0 : 1)
            : Math.Clamp((position - leftPosition) / (rightPosition - leftPosition), 0, 1);
        if (Interpolation == "Constant") return WithAlpha(a);
        if (Interpolation == "Ease") t = t * t * (3 - 2 * t);
        if (ColorMode != "RGB")
        {
            var x = ToHue(a.Color, ColorMode == "HSL"); var y = ToHue(b.Color, ColorMode == "HSL");
            if (x[1] < 1e-8) x[0] = y[0];
            if (y[1] < 1e-8) y[0] = x[0];
            var delta = y[0] - x[0];
            if (HueInterpolation == "Near") { if (delta > .5) delta -= 1; if (delta < -.5) delta += 1; }
            if (HueInterpolation == "Far") { if (delta >= 0 && delta < .5) delta -= 1; else if (delta < 0 && delta > -.5) delta += 1; }
            if (HueInterpolation == "Clockwise" && delta < 0) delta += 1;
            if (HueInterpolation == "Counterclockwise" && delta > 0) delta -= 1;
            var c = FromHue(x[0] + delta * t, Lerp(x[1], y[1], t), Lerp(x[2], y[2], t), ColorMode == "HSL");
            return Color.FromArgb(Byte(Lerp(a.Alpha, b.Alpha, t) * 255), c);
        }
        double Channel(Func<RampStop, double> get)
        {
            if (Interpolation is not ("Cardinal" or "B-Spline")) return Lerp(get(a), get(b), t);
            var p0 = get(ordered[Math.Max(0, right - 2)]); var p1 = get(a); var p2 = get(b); var p3 = get(ordered[Math.Min(ordered.Length - 1, right + 1)]);
            var t2 = t * t; var t3 = t2 * t;
            if (Interpolation == "B-Spline") return ((-t3 + 3*t2 - 3*t + 1)*p0 + (3*t3 - 6*t2 + 4)*p1 + (-3*t3 + 3*t2 + 3*t + 1)*p2 + t3*p3) / 6;
            // Blender's Cardinal basis uses tension 0.71.
            const double k = .71;
            return (-k*t3 + 2*k*t2 - k*t)*p0 + ((2-k)*t3 + (k-3)*t2 + 1)*p1 + ((k-2)*t3 + (3-2*k)*t2 + k*t)*p2 + (k*t3-k*t2)*p3;
        }
        return Color.FromArgb(Byte(Channel(s => s.Alpha * 255)), Byte(Channel(s => s.Color.R)), Byte(Channel(s => s.Color.G)), Byte(Channel(s => s.Color.B)));
    }
    static Color WithAlpha(RampStop s) => Color.FromArgb(Byte(s.Alpha * 255), s.Color);
    static int Byte(double x) => (int)Math.Round(Math.Clamp(x, 0, 255));
    static double Lerp(double a, double b, double t) => a + (b-a)*t;
    static double[] ToHue(Color c, bool hsl)
    {
        double r=c.R/255.0, g=c.G/255.0, b=c.B/255.0, max=Math.Max(r,Math.Max(g,b)), min=Math.Min(r,Math.Min(g,b)), d=max-min;
        double h=d == 0 ? 0 : max == r ? ((g-b)/d)%6 : max == g ? (b-r)/d+2 : (r-g)/d+4;
        h = (h/6+1)%1;
        double l=(max+min)/2;
        return [h, hsl ? (d==0 ? 0 : d/(1-Math.Abs(2*l-1))) : (max==0 ? 0 : d/max), hsl ? l : max];
    }
    static Color FromHue(double h, double s, double v, bool hsl)
    {
        h=(h%1+1)%1;
        double c=hsl ? (1-Math.Abs(2*v-1))*s : v*s, x=c*(1-Math.Abs(h*6%2-1)), m=hsl ? v-c/2 : v-c;
        (double r,double g,double b) = (int)(h*6) switch { 0 => (c,x,0d), 1 => (x,c,0d), 2 => (0d,c,x), 3 => (0d,x,c), 4 => (x,0d,c), _ => (c,0d,x) };
        return Color.FromArgb(Byte((r+m)*255),Byte((g+m)*255),Byte((b+m)*255));
    }
}
