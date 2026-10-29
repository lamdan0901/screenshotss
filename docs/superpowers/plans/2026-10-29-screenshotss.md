# Screenshotss Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline. Steps use checkbox syntax.

**Goal:** Save selected screen regions to a remembered folder and copy the full path.
**Architecture:** WinForms ApplicationContext owns the tray and hotkey window.
A modal overlay selects from a frozen desktop; ScreenshotStore owns PNG writing
and AppSettings owns persisted folder selection.
**Tech Stack:** C#, .NET 10, WinForms, built-in Win32 APIs; no packages.
**Spec:** ../specs/2026-10-29-screenshotss-design.md

## Global Constraints

- Windows desktop, PerMonitorV2, Ctrl+Shift+F9, PNG, full path as plain text.
- No overwrites, no clipboard changes on cancellation/save failure.
- User verification preference: compiler/analyzer first, stop if that fails;
  build only as necessary to produce the runnable app.

## Review Focus

- Negative screen coordinates and reverse/out-of-bounds drags: geometry assertions.
- Duplicate filenames: save repeatedly and confirm earlier image bytes survive.
- Unwritable folder: reject without claiming success or copying a path.
- Malformed settings: preserve the file and show a recovery message.
- Clipboard busy: bounded retry, retain PNG, show its recoverable path.

### Task 1: File output and settings

Files: Screenshotss/Screenshotss.csproj, ScreenshotStore.cs, AppSettings.cs,
SelectionGeometry.cs; checks/Screenshotss.Checks.csproj, Program.cs.

- [x] Add assertion cases for PNG dimensions/pixels, unique names, absolute
  paths, invalid targets, settings round trip, malformed JSON and geometry.
- [x] Implement Save(Bitmap, string), Load(string), Save(string, string),
  and Between(Point, Point, Size) with the behavior specified above.

### Task 2: Tray and capture

Files: Screenshotss/Program.cs, TrayApplication.cs, CaptureOverlay.cs,
HotkeyWindow.cs; README.md, .gitignore.

- [x] Add a single-instance STA entry point and PerMonitorV2 initialization.
- [x] Wire tray commands, first-run picker, global hotkey and PNG/path flow.
- [x] Draw a frozen desktop and dim outside the dragged selection; cancel on
  Escape/right-click/focus loss. Dispose unmanaged and bitmap resources.
- [x] Add launch instructions and known capture limitations.

### Task 3: Verification and executable

- [x] Run compiler/analyzer checks; stop verification if they fail.
- [x] Compile the executable and assertion runner only to deliver/run them.
- [x] Run assertions and document unavailable native UI checks.
- [x] Review all changed files and report exact validation limits.

## Execution record

- Workspace was empty and is not a Git checkout; no worktree/commits required.
- User selected the recommended C# tray approach; proceed inline on that scope.
- `dotnet build checks/Screenshotss.Checks.csproj -c Release -warnaserror`:
  zero warnings/errors. Compilation was necessary to deliver the executable.
- Assertion runner: all 21 checks passed. No third-party test dependencies.
- Copied app executable, DLL, deps and runtime config into dist (185,269 bytes).
- Native UI automation was not available in this turn (no node_repl tool).
  No manual desktop or mixed-DPI hardware verification claimed.
- Independent reviewer found exclusive endpoints omitted the final desktop row
  and column. Fixed inclusive cursor endpoints, preserving click/line cancellation.
  Added forward/reverse corner-drag checks; compiler and all 21 checks passed again.
