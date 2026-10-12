# Color Ramp

A compact standalone Windows color ramp editor inspired by Blender 5.1, with a screen eyedropper that works across connected monitors. First public early release: v0.1.0.

![Approved design](design/approved-mockup.png)

## Run

Download `ColorRamp-v0.1.0-win-x64.exe` from the [GitHub release](https://github.com/katfacey/color-ramp/releases/tag/v0.1.0) and run it. It includes the .NET runtime, so no installation or Blender is needed. The local release is in `releases/v0.1.0/`; the development build remains in `dist/ColorRamp.exe`.

The packaged x64 build includes its .NET runtime. Developer builds need the .NET 8 Windows Desktop runtime (or the .NET 8/9 SDK).

## Using the editor

- Click a stop to select it; drag it to reposition. Drag **Pos** or **Alpha** left/right from anywhere in the field to adjust relative to its current value. Scroll while hovering to increase/decrease, or double-click the field to type an exact value.
- **+** inserts a stop between the selected stop and its predecessor (or the next stop when the first is selected). **−** removes the selected stop. A ramp supports 2 to 32 stops.
- Double-click the ramp to add a marker at that position, using the sampled ramp color and alpha. Right-click the ramp for **Copy color HEX** and **Place marker** at that location. Click the color swatch to open the selected color dialog. Enter six HEX digits directly or adjust alpha from 0 to 1.
- **The eyedropper beside the color swatch** hides the editor and shows a magnified pixel preview over a frozen capture of each display. Move between monitors freely. Click or press Enter to apply, Esc or right-click to cancel. Arrow keys move one physical pixel.
- **Copy color** copies the selected stop's six-digit HEX value. **Save ramp** saves the editable settings as `.colorramp.json`.
- The **⋯** menu has Open, flip, distribute, undo/redo, PNG export, and System/Light/Dark theme choices.
- **📌** keeps the editor above other windows; a check mark indicates it is pinned. System theme follows Windows' app theme.

| Shortcut | Action |
|---|---|
| E | Pick screen (outside a text field) |
| Ctrl+S / Ctrl+O | Save / open ramp |
| Ctrl+Z / Ctrl+Y | Undo / redo (HEX text retains native text undo) |
| Ctrl+C | Copy selected color (outside a text field) |
| Delete | Delete stop (outside a text field) |

The last ramp and theme/pin choices are saved in `%LOCALAPPDATA%\ColorRamp`. Screen captures are held only in memory during picking and are discarded when the picker closes. No network access is made by the app.

## Color behavior and current limits

RGB, HSV and HSL modes are supported. RGB interpolation supports Linear, Constant, Ease, Cardinal and B-Spline. HSV/HSL use Linear, Constant or Ease and expose Near/Far/Clockwise/Counterclockwise hue paths. PNG export preserves alpha.

B-Spline blends before the first stop and after the last stop using Blender-style virtual endpoint stops. A stop's displayed color may differ from its own swatch because B-Spline smooths neighboring colors rather than passing through every swatch exactly.

The screen-picking button beside the color swatch uses an eyedropper icon. The original vector and transparent 64×64 theme variants are in `assets/`; the artwork is CC0 with no attribution requirement. See `assets/LICENSE.md` for the source record.

Screen capture uses DirectX desktop duplication, preserving HDR pixels as floating-point scRGB before converting to the ramp's SDR sRGB range. Each display's Windows SDR white level is used to keep ordinary desktop colors consistent across HDR and SDR screens. Highlights outside the SDR range are scaled together to retain their hue. The picker waits for an actual desktop image instead of a cursor-only update, and checks empty black captures against a GDI fallback. A GDI capture fallback is available when DirectX capture is unavailable; this fallback may clip HDR content.

This is an independent app, not an official Blender product or an exact reproduction of Blender's color-management pipeline. Interpolation uses 8-bit display RGB values; Blender may interpolate scene-linear values and apply view transforms. Colors may differ under HDR, wide-gamut or ICC color management. Protected video, secure desktops and some hardware overlays cannot be sampled reliably. Captures freeze the desktop at the time picking begins; restart picking to refresh changing content.

The main interface uses rounded buttons and fields. Stop selection uses left/right chevrons rather than dragging; position and alpha remain sliders. The optional Blender-style color picker includes a hue/saturation wheel, brightness strip, HEX entry and alpha. Toggle "Use Blender-style color picker" in the tools menu to switch between it and the native Windows dialog. This choice is saved between sessions. HEX copying, editable JSON saving and PNG export are implemented; a saved-ramp library and Blender import/export are not yet included.

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

## AI disclaimer

This project was coded with AI assistance using OpenAI Codex.
