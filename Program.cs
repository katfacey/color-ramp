using System.Text.Json;

namespace ColorRamp;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (_, e) => {
            try { Directory.CreateDirectory(Storage.DirectoryPath); File.AppendAllText(Path.Combine(Storage.DirectoryPath,"errors.log"),$"{DateTimeOffset.Now:O}\n{e.Exception}\n"); } catch { }
            MessageBox.Show("Color Ramp encountered an error.\n"+e.Exception.Message,"Color Ramp",MessageBoxButtons.OK,MessageBoxIcon.Error);
        };
        if (args.Contains("--self-test"))
        {
            var output = args.SkipWhile(a => a != "--self-test").Skip(1).FirstOrDefault() ?? "self-test.txt";
            try { File.WriteAllText(output, SelfTest.Run()); }
            catch (Exception ex) { File.WriteAllText(output, ex.ToString()); Environment.ExitCode = 1; }
            return;
        }
        if(args.Contains("--preview-picker"))
        {
            using var dialog=new ColorPickerDialog(Color.FromArgb(91,137,204),.85,!args.Contains("--light"));
            dialog.Show();Application.DoEvents();
            using var bitmap=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,dialog.Size));
            bitmap.Save(args[Array.IndexOf(args,"--preview-picker")+1],System.Drawing.Imaging.ImageFormat.Png);dialog.Close();return;
        }
        if (args.Contains("--preview"))
        {
            var output = args[Array.IndexOf(args, "--preview") + 1];
            using var form = new MainForm(false);
            int rampArgument = Array.IndexOf(args,"--ramp");
            if (rampArgument >= 0 && rampArgument + 1 < args.Length) form.SetRampForPreview(Storage.Read(args[rampArgument+1]));
            form.SetTheme(args.Contains("--light") ? "Light" : "Dark");
            form.Show();
            Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(output, System.Drawing.Imaging.ImageFormat.Png);
            form.Close();
            return;
        }
        Application.Run(new MainForm());
    }
}

internal static class Storage
{
    public static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorRamp");
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static RampDocument Read(string path)
    {
        var document = JsonSerializer.Deserialize<RampDocument>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("This file does not contain a ramp.");
        document.Validate();
        return document;
    }
    public static void Write(string path, RampDocument document)
    {
        document.Validate();
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(document, JsonOptions));
        File.Move(temporary, path, true);
    }
}
