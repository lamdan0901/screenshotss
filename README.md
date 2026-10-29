# Screenshotss

A small native Windows tray app. Drag a region, automatically save a PNG to
your chosen folder, and copy the **full file path as text** to the clipboard.
Or capture into the annotation editor and save when you are done drawing.

## Run

Requires Windows 10/11 and the **.NET 10 Desktop Runtime** (the Windows Desktop
runtime, not only the ASP.NET/Core runtime). No third-party packages or installer.

Double-click the included `dist/Screenshotss.exe`, or run from source:

```powershell
dotnet run --project Screenshotss
```

1. Choose a save folder on first launch. The app then stays in the system tray;
   look under the taskbar's hidden icons if needed.
2. Press **Ctrl+Shift+F9**, double-click its tray icon, or right-click → **Capture**.
3. Drag a rectangle; release to save and copy its absolute path.
4. Paste the path with **Ctrl+V**. Escape/right-click cancels without saving or
   changing the clipboard. A click without a drag leaves the selector open.

Right-click the tray icon for **Choose folder**, **Open folder**, and **Exit**.
The folder is remembered in `%LOCALAPPDATA%\Screenshotss\settings.json`.
If you cancel the first folder picker, Capture asks again when you use it.

The path is copied after saving succeeds. If another app keeps the clipboard
busy, Screenshotss retries, then shows where the saved image can be found.
Captures have timestamped, unique names and do not overwrite existing images.
If the selected folder disappears, choose it again; the app does not silently
save elsewhere. A shortcut conflict leaves the tray Capture command available.

## Capture and annotate

Press **Ctrl+Shift+F10** or right-click the tray icon → **Capture and annotate**.
Select a region as usual, then draw in the editor:

- **Pen**, **Line**, **Arrow**: drag to draw. Arrows point toward the release point.
- The color swatch opens the native color picker. The **stroke thickness** icon
  opens a popup below the toolbar with a slider and live preview side by side.
  Drag the slider to select 1–32 image pixels; the preview uses the current color.
  Each new stroke keeps the color and width chosen when it starts.
- **Undo** or **Ctrl+Z** removes the last stroke.
- **Save** or **Ctrl+S** saves the annotated PNG, copies its full path, and closes
  the editor. Nothing is saved or copied before this step.
- **Discard**, **Esc**, or the close button immediately discards the unsaved capture.

The preview fits the window without changing saved image dimensions. Save
errors keep the editor open so you can retry. If the destination folder has
disappeared, Save lets you choose another folder. The desktop buffer is released
before editing; only the selected capture and its annotations remain in memory.

**Ctrl+Shift+F9** and tray double-click continue to save immediately.

### Updating a running copy

The latest build is in `dist/Screenshotss.exe`. Exit the old app through its
tray menu, then launch this executable. Keep all files in `dist` together.
The remembered save folder is shared with the old copy, including `dist-update`.
Only one instance can run at a time.

The executable, system tray and annotation window use the artwork from
`icon.svg`, converted to `Screenshotss/Assets/app.ico` with sizes from 16 to
256 px. The icon is embedded in the executable; no separate icon files are
needed to run the app.

Annotation toolbar buttons use the supplied SVGs in `icons/` for Pen, Line,
Arrow, Undo, Save and Discard. Their transparent PNG versions are embedded from
`Screenshotss/Assets/Toolbar/`; no loose toolbar images are needed at runtime.
Toolbar buttons use icons, with names and shortcuts in tooltips. Color shows
the selected drawing color, and Discard is red. The thickness trigger uses
`icons/line-thickness.svg`; its popup shows the current width in px.

## Compile and check

The .NET 10 SDK is needed for source builds. The compiler and built-in analyzers
run with warnings treated as errors:

```powershell
dotnet build Screenshotss/Screenshotss.csproj -c Release -warnaserror -o dist
dotnet run --project checks/Screenshotss.Checks.csproj -c Release
```

To check the thickness popup's native layout and live preview after compiling:

```powershell
pwsh -NoProfile -STA -File checks/thickness-popup.ps1
```

Keep the entire `dist` folder together when moving the app. This is a small
framework-dependent build; it does not bundle the .NET runtime.

The dependency-free checks cover PNG pixels and dimensions, unique files,
Unicode paths, folder persistence, invalid settings/destinations, and selection
geometry across negative monitor coordinates. Annotation checks cover preview
coordinate mapping, pen/line/arrow pixels, width/color, undo rendering, and
original-size PNG output.

## Capture limits

Region capture uses a frozen desktop image and physical-pixel coordinates with
PerMonitorV2 awareness. The overlay supports the full virtual desktop, including
monitors left of or above the primary screen. Actual mixed-DPI hardware still
needs a manual check. Desktop buffers exist only during capture.

The cursor is not included. Protected/DRM content and secure desktops cannot be
captured reliably. GDI capture does not provide HDR color fidelity; use SDR for
predictable colors. Selection currently uses a mouse; Escape cancels from the
keyboard. Annotation tools currently require a mouse to draw. Unsaved captures
are not recovered after an app/OS crash. No text, eraser, redo, recording, OCR,
autostart, or cloud integration.

Windows APIs: [CopyFromScreen](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.graphics.copyfromscreen),
[RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey),
[Clipboard.SetDataObject](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.clipboard.setdataobject).
