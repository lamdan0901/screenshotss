# Screenshotss

Build a small Windows tray app: select a folder once, press Ctrl+Shift+F9 or
Capture, drag a rectangle, then save a PNG and copy its absolute path as text.
Escape/right-click cancels without creating a file or changing the clipboard.

## Approach

The user selected C# and WinForms. C++/Win32 would reduce deployment footprint
but require more implementation code. Use .NET 10, built-in desktop APIs, and
no third-party dependencies. Deliver a framework-dependent executable folder;
the target PC needs the .NET 10 Desktop Runtime.

## Behavior and boundaries

- Tray menu: Capture, Choose folder, Open folder, Exit. Double-click captures.
- First launch asks for the save folder; cancelling leaves Capture available.
- Remember the folder under LocalAppData/Screenshotss/settings.json.
- Capture a frozen desktop before showing a dimmed selection overlay.
- Use physical pixels, the virtual desktop, and PerMonitorV2 DPI awareness.
- Save unique PNG files without overwriting existing files. Only copy the path
  after saving completes. Report clipboard failure separately; retain the image.
- Handle hotkey conflicts, invalid settings, inaccessible folders, and a second
  instance with clear messages. Release screenshot buffers and tray resources.
- Ctrl+Shift+F9 is the implementation default; F12 is reserved by Windows.
- No editor, OCR, recording, autostart, installer, or cloud service.

## Verification

Use compiler/analyzer checks first. A build is necessary once to deliver the
executable for this new native app. Keep a dependency-free assertion runner for
PNG output, folder persistence, no-overwrite behavior, invalid paths, and
selection geometry. Native UI checks cover selection/cancel if the available
desktop automation supports them. Report hardware coverage limits explicitly.
