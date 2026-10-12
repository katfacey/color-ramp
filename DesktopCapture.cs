using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ColorRamp;

internal static class DesktopCapture
{
    public static string LastCaptureInfo { get; private set; } = "";
    public static Bitmap Capture(Rectangle bounds)
    {
        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        for (uint a = 0; factory.EnumAdapters1(a, out var adapter).Success; a++)
        using (adapter)
        {
            for (uint o = 0; adapter.EnumOutputs(o, out var output).Success; o++)
            using (output)
            {
                var desc = output.Description;
                var rect = desc.DesktopCoordinates;
                if (rect.Left != bounds.Left || rect.Top != bounds.Top || rect.Right != bounds.Right || rect.Bottom != bounds.Bottom) continue;
                D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.BgraSupport, [FeatureLevel.Level_11_0], out ID3D11Device device, out ID3D11DeviceContext context).CheckError();
                using (device) using (context) using (var output5 = output.QueryInterface<IDXGIOutput5>())
                using (var duplication = CreateDuplication(output5, device))
                {
                    duplication.AcquireNextFrame(1500, out _, out var resource).CheckError();
                    try
                    {
                        using (resource) using (var texture = resource.QueryInterface<ID3D11Texture2D>())
                        {
                            var description = texture.Description;
                            description.Usage = ResourceUsage.Staging; description.BindFlags = BindFlags.None;
                            description.CPUAccessFlags = CpuAccessFlags.Read; description.MiscFlags = ResourceOptionFlags.None;
                            using var staging = device.CreateTexture2D(description);
                            context.CopyResource(staging, texture);
                            var mapped = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                            try
                            {
                                float white = SdrWhiteScale(desc.DeviceName);
                                var result = Convert(mapped, description, white);
                                switch (desc.Rotation)
                                {
                                    case ModeRotation.Rotate90: result.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                                    case ModeRotation.Rotate180: result.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                                    case ModeRotation.Rotate270: result.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
                                }
                                if (result.Size != bounds.Size) { result.Dispose(); throw new InvalidOperationException("Unexpected display capture dimensions."); }
                                LastCaptureInfo = $"{desc.DeviceName}: {description.Format}; SDR white scale {white:F3}";
                                return result;
                            }
                            finally { context.Unmap(staging, 0); }
                        }
                    }
                    finally { duplication.ReleaseFrame(); }
                }
            }
        }
        throw new InvalidOperationException("Display not available for DirectX capture.");
    }
    static unsafe IDXGIOutputDuplication CreateDuplication(IDXGIOutput5 output, ID3D11Device device)
    {
        // Call IDXGIOutput5::DuplicateOutput1 directly. The 3.6.2 wrapper's array
        // overload crashes in native code; this uses the documented COM ABI.
        Format* formats = stackalloc Format[] { Format.R16G16B16A16_Float, Format.B8G8R8A8_UNorm };
        IntPtr result;
        var table = *(IntPtr**)output.NativePointer;
        var call = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, uint, Format*, IntPtr*, int>)table[26];
        Marshal.ThrowExceptionForHR(call(output.NativePointer, device.NativePointer, 0, 2, formats, &result));
        return new IDXGIOutputDuplication(result);
    }
    static unsafe Bitmap Convert(MappedSubresource mapped, Texture2DDescription desc, float white)
    {
        var bitmap = new Bitmap((int)desc.Width, (int)desc.Height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < bitmap.Height; y++)
            {
                byte* input = (byte*)mapped.DataPointer + y * mapped.RowPitch;
                byte* destination = (byte*)data.Scan0 + y * data.Stride;
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (desc.Format == Format.R16G16B16A16_Float)
                    {
                        Half* pixel = (Half*)(input + x * 8);
                        var color = ToSdr((float)pixel[0], (float)pixel[1], (float)pixel[2], white);
                        destination[x * 4] = color.B; destination[x * 4 + 1] = color.G; destination[x * 4 + 2] = color.R;
                    }
                    else if (desc.Format is Format.B8G8R8A8_UNorm or Format.R8G8B8A8_UNorm)
                    {
                        bool bgra = desc.Format == Format.B8G8R8A8_UNorm;
                        destination[x * 4] = input[x * 4 + (bgra ? 0 : 2)];
                        destination[x * 4 + 1] = input[x * 4 + 1];
                        destination[x * 4 + 2] = input[x * 4 + (bgra ? 2 : 0)];
                    }
                    else throw new NotSupportedException($"Unsupported capture format: {desc.Format}");
                    destination[x * 4 + 3] = 255;
                }
            }
        }
        catch { bitmap.UnlockBits(data); bitmap.Dispose(); throw; }
        bitmap.UnlockBits(data); return bitmap;
    }
    internal static Color ToSdr(float r, float g, float b, float white)
    {
        white = float.IsFinite(white) && white > 0 ? white : 1;
        r = Math.Max(0, float.IsFinite(r) ? r / white : 0);
        g = Math.Max(0, float.IsFinite(g) ? g / white : 0);
        b = Math.Max(0, float.IsFinite(b) ? b / white : 0);
        // Preserve highlight hue when a color exceeds the SDR ramp's range.
        float peak = Math.Max(1, Math.Max(r, Math.Max(g, b)));
        static int Encode(float c) => (int)Math.Round(255 * (c <= .0031308f ? 12.92f * c : 1.055f * MathF.Pow(c, 1 / 2.4f) - .055f));
        return Color.FromArgb(Encode(r / peak), Encode(g / peak), Encode(b / peak));
    }
    [DllImport("user32.dll")] static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll")] static extern int QueryDisplayConfig(uint flags, ref uint paths, IntPtr pathData, ref uint modes, IntPtr modeData, IntPtr topology);
    [DllImport("user32.dll")] static extern int DisplayConfigGetDeviceInfo(IntPtr request);
    static float SdrWhiteScale(string name)
    {
        const uint active = 2;
        if (GetDisplayConfigBufferSizes(active, out var paths, out var modes) != 0) return 1;
        IntPtr pathData = Marshal.AllocHGlobal(checked((int)paths * 72)), modeData = Marshal.AllocHGlobal(checked((int)modes * 64)), request = Marshal.AllocHGlobal(84);
        try
        {
            if (QueryDisplayConfig(active, ref paths, pathData, ref modes, modeData, IntPtr.Zero) != 0) return 1;
            for (int i = 0; i < paths; i++)
            {
                IntPtr path = pathData + i * 72;
                void Header(int type, int size, int offset)
                {
                    Marshal.WriteInt32(request, 0, type); Marshal.WriteInt32(request, 4, size);
                    Marshal.WriteInt64(request, 8, Marshal.ReadInt64(path, offset));
                    Marshal.WriteInt32(request, 16, Marshal.ReadInt32(path, offset + 8));
                }
                Header(1, 84, 0);
                if (DisplayConfigGetDeviceInfo(request) != 0 || !string.Equals(Marshal.PtrToStringUni(request + 20), name, StringComparison.OrdinalIgnoreCase)) continue;
                Header(11, 24, 20);
                if (DisplayConfigGetDeviceInfo(request) == 0) return Math.Max(1, Marshal.ReadInt32(request, 20) / 1000f);
            }
        }
        finally { Marshal.FreeHGlobal(pathData); Marshal.FreeHGlobal(modeData); Marshal.FreeHGlobal(request); }
        return 1;
    }
}





