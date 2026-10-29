# Screenshotss

Build a small Windows tray app: select a folder once, press PrintScreen or
Capture, drag a rectangle, then save a PNG and copy its absolute path as text.
Escape/right-click cancels without creating a file or changing the clipboard.

## Approach

The user selected C# and WinForms. C++/Win32 would reduce deployment footprint
but require more implementation code. Use .NET 10, built-in desktop APIs, and
no third-party dependencies. Deliver a framework-dependent executable folder;
the target PC needs the .NET 10 Desktop Runtime.

## Behavior and boundaries

- Tray menu: Capture, Capture and annotate, Fullscreen capture, Capture all
  screens (fullscreen), Choose folder, Open folder, Start with Windows, Exit.
  Double-click captures a region.
- First launch asks for the save folder; cancelling leaves Capture available.
- Remember the folder under LocalAppData/Screenshotss/settings.json.
- Capture a frozen desktop before showing a dimmed selection overlay.
- Use physical pixels, the virtual desktop, and PerMonitorV2 DPI awareness.
- Save unique PNG files without overwriting existing files. Only copy the path
  after saving completes. Report clipboard failure separately; retain the image.
- Handle hotkey conflicts, invalid settings, inaccessible folders, and a second
  instance with clear messages. Release screenshot buffers and tray resources.
- PrintScreen selects a region; Shift+PrintScreen selects a region to annotate.
- Ctrl+Shift+PrintScreen saves the monitor under the cursor immediately and
  copies its path, without selection or editing. The Capture all screens toggle
  captures the full virtual desktop instead; it starts unchecked each launch.
- Start with Windows is on by default. Store a quoted executable path in the
  current user's Run registry entry; unchecking removes only Screenshotss's entry.
  Remember an explicit Off choice separately so later launches do not re-enable it.
  Read the existing registration on launch and report registry failures.
- No OCR, recording, installer, or cloud service.

## Verification

Use compiler/analyzer checks first. A build is necessary once to deliver the
executable for this new native app. Keep a dependency-free assertion runner for
PNG output, folder persistence, no-overwrite behavior, invalid paths, and
selection geometry. Native UI checks cover selection/cancel if the available
desktop automation supports them. Report hardware coverage limits explicitly.
