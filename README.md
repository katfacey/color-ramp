# Color Ramp

A compact standalone Windows color ramp editor inspired by Blender 5.1, with a screen eyedropper that works across connected monitors. Current early release: v0.1.1.

![Approved design](design/approved-mockup.png)

## Run

Download `ColorRamp-v0.1.1-win-x64.exe` from the [GitHub release](https://github.com/katfacey/color-ramp/releases/tag/v0.1.1) and run it. It includes the .NET runtime, so no installation or Blender is needed. The local release is in `releases/v0.1.1/`; the development build remains in `dist/ColorRamp.exe`.

The packaged x64 build includes its .NET runtime. Developer builds need the .NET 8 Windows Desktop runtime (or the .NET 8/9 SDK).

## Using the editor

- **Select and move:** Click a marker to select it, then drag it along the ramp. Use the arrows beside the stop number to select the previous or next marker.
- **Add and remove:** Double-click the ramp to add a marker with the color at that point, or use **+**. Use **−** to remove the selected marker. Right-click the ramp to place a marker or copy the color there. Ramps support 2–32 markers.
- **Adjust position and opacity:** Drag **Pos** or **Alpha** left/right, scroll while hovering, or double-click to type a value. Alpha runs from 0 (transparent) to 1 (opaque).
- **Choose a color:** Click the color swatch or enter a HEX value. The **⌄** menu lets you switch between the Blender-style picker and the Windows picker.
- **Pick from your screen:** Click the eyedropper beside the swatch. Click or press Enter to choose a pixel; press Esc or right-click to cancel. Arrow keys move one pixel. Picking works across connected monitors.
- **Copy and save:** **📋 Copy color** copies the selected color as HEX. **💾 Save ramp** saves an editable `.colorramp.json` file. The **⌄** menu also offers Open, PNG export, flip, distribute, undo/redo, and theme settings.
- **Keep it visible:** **📌** toggles always-on-top; a check mark means it is enabled. Choose System theme to follow Windows' light/dark setting.

Your last ramp, theme, pin, and picker choices are saved automatically in `%LOCALAPPDATA%\ColorRamp`. The app makes no network requests; screen captures stay in memory and are discarded after picking.

| Shortcut | Action |
|---|---|
| E | Screen eyedropper |
| Ctrl+S / Ctrl+O | Save / open ramp |
| Ctrl+Z / Ctrl+Y | Undo / redo |
| Ctrl+C | Copy selected color |
| Delete | Delete selected marker |

E, Ctrl+C, and Delete apply when you are not editing a field. HEX text keeps its own undo behavior.

## Color behavior and current limits

- **Color modes:** RGB supports Linear, Constant, Ease, Cardinal, and B-Spline interpolation. HSV/HSL support Linear, Constant, and Ease, with four hue directions. PNG export preserves transparency.
- **B-Spline:** Smooths neighboring colors, so the ramp may not match a marker's swatch exactly. Blending extends beyond the first and last markers, as in Blender.
- **Color matching:** This independent app is inspired by Blender but uses a different color-management pipeline. HDR, wide-gamut, and display profiles can affect the colors you see and sample.
- **Screen capture:** HDR captures are converted to SDR sRGB using each display's SDR white level. The fallback capture method may clip HDR colors. Protected video, secure desktops, and some overlays cannot be sampled reliably.
- **Frozen preview:** Picking freezes each screen when it starts. Restart the eyedropper to capture updated content.
- **Not yet available:** A saved-ramp library and Blender import/export.

## Build and verify

```powershell
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
```

For the built-in verification suite:

```powershell
Start-Process .\bin\Release\net8.0-windows\ColorRamp.exe -ArgumentList '--self-test artifacts/self-test.txt' -Wait
Get-Content artifacts/self-test.txt
```

The checks cover interpolation/alpha, hue paths, duplicate stops, invalid data, independent undo snapshots, file round trips, layout, and a capture of each attached monitor. Captures are discarded, not saved. Live manual verification should include dragging stops and picking across displays with differing scaling settings.

Sources: [Blender 5.1 Color Ramp widget](https://docs.blender.org/manual/en/5.1/interface/controls/templates/color_ramp.html), [Windows HDR color](https://learn.microsoft.com/en-us/windows/win32/direct3darticles/high-dynamic-range), [Windows SDR white level](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_sdr_white_level). DirectX bindings use [Vortice.Windows](https://github.com/amerkoleci/Vortice.Windows), licensed under MIT.

## License

Color Ramp is released under the [MIT license](LICENSE). Third-party notices are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md), and icon provenance is in [assets/LICENSE.md](assets/LICENSE.md).

## AI disclaimer

This project was coded with AI assistance using OpenAI Codex.
