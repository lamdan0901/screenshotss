# Screenshotss

A small native Windows tray app. Drag a region, automatically save a PNG to
your chosen folder, then copy the **full file path as text** to the clipboard
first and the **image itself** second.
Or capture into the annotation editor and save when you are done drawing.

## Run

Requires Windows 10 version 1809 or later / Windows 11 and the **.NET 10 Desktop Runtime** (the Windows Desktop
runtime, not only the ASP.NET/Core runtime). No third-party packages or installer.

Double-click the included `dist/Screenshotss.exe`, or run from source:

```powershell
dotnet run --project Screenshotss
```

1. Choose a save folder on first launch. The app then stays in the system tray;
   look under the taskbar's hidden icons if needed.
2. Press **PrintScreen**, double-click its tray icon, or right-click → **Capture**.
3. Drag a rectangle; release to save, copy its absolute path, then copy the image.
4. Paste the image with **Ctrl+V**. Escape/right-click cancels without saving or
   changing the clipboard. A click without a drag leaves the selector open.

Right-click the tray icon for **Choose folder**, **Open folder**, and **Exit**.
The folder is remembered in `%LOCALAPPDATA%\Screenshotss\settings.json`.
If you cancel the first folder picker, Capture asks again when you use it.

The path and image are copied in that order after saving succeeds. With Windows
clipboard history enabled, the app waits until the path is retained before
copying the image, giving **Win+V** separate image and path entries. **Ctrl+V**
pastes the image. If history is disabled, only the final image remains available.
If Windows cannot retain the path within five seconds, the path stays on the
clipboard and the app reports the problem; the PNG is still saved. Clipboard
access retries if another app holds it open. Windows clipboard history limits
each item to 4 MB, so large images may only be available through Ctrl+V.
Captures have timestamped, unique names and do not overwrite existing images.
If the selected folder disappears, choose it again; the app does not silently
save elsewhere. A shortcut conflict leaves the tray Capture command available.

## Capture and annotate

Press **Shift+PrintScreen** or right-click the tray icon → **Capture and annotate**.
Select a region as usual, then draw in the editor:

- **Pen**, **Line**, **Arrow**: drag to draw. Arrows point toward the release point.
- The color swatch opens the native color picker. The **stroke thickness** icon
  opens a popup below the toolbar with a slider and live preview side by side.
  Drag the slider to select 1–32 image pixels; the preview uses the current color.
  Each new stroke keeps the color and width chosen when it starts.
- **Undo** or **Ctrl+Z** removes the last stroke.
- **Save** or **Ctrl+S** saves the annotated PNG, copies its full path followed by
  the annotated image, and closes the editor. Nothing is saved or copied before
  this step.
- **Discard**, **Esc**, or the close button immediately discards the unsaved capture.

The preview fits the window without changing saved image dimensions. Save
errors keep the editor open so you can retry. If the destination folder has
disappeared, Save lets you choose another folder. The desktop buffer is released
before editing; only the selected capture and its annotations remain in memory.

**PrintScreen** and tray double-click continue to save immediately.

## Fullscreen capture

Press **Ctrl+Shift+PrintScreen** or right-click the tray icon → **Fullscreen capture**.
The monitor under the cursor is saved immediately as a PNG, and its full path
is copied followed by the image. No region selection or annotation editor opens.

Enable **Capture all screens (fullscreen)** in the tray menu to capture all
monitors together. This toggle starts unchecked each time the app launches
and only affects fullscreen capture.

## Start with Windows

**Start with Windows** is enabled automatically on first launch, so Screenshotss
launches when you sign into Windows. Uncheck it in the tray menu to remove startup
registration. Your choice persists across app restarts; no admin is needed.

Startup uses the current executable's location. Keep that folder in place.
If you move the app or switch between `dist` and `dist-update`, turn the option
off and on from the copy you want Windows to launch.

### Updating a running copy

Updated builds are in `dist/Screenshotss.exe`, or `dist-update/Screenshotss.exe`
when the old app is running from `dist`. Exit the old app through its tray menu,
then launch the updated executable. Keep all files in its folder together.
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
dotnet build Screenshotss/Screenshotss.csproj -c Release -warnaserror --no-incremental -o dist
dotnet run --project checks/Screenshotss.Checks.csproj -c Release
```

Use the full build pipeline when distributing the app. Calling MSBuild's
`Compile` target alone skips embedded-resource preparation; a subsequent
incremental build can reuse a DLL without the toolbar icons.

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
original-size PNG output. Fullscreen checks briefly capture the cursor's monitor
and the entire desktop in memory and check dimensions without saving or copying.
Startup checks use an isolated temporary registry key, leaving actual Windows
startup entries untouched.

To check clipboard write order, retained image pixels, and actual Windows
history entries (these replace the clipboard with a small test image; enable
Windows clipboard history before running):

```powershell
pwsh -NoProfile -STA -File checks/clipboard.ps1
dotnet run --project checks/Screenshotss.Checks.csproj -c Release -- --clipboard-history
```

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
or cloud integration.

Windows APIs: [CopyFromScreen](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.graphics.copyfromscreen),
[RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey),
[Clipboard.SetDataObject](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.clipboard.setdataobject),
[Run registry keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys).
