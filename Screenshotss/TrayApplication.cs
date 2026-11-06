using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Screenshotss;

internal sealed class TrayApplication : ApplicationContext
{
    private readonly ContextMenuStrip menu = new();
    private readonly ToolStripMenuItem captureAllScreens = new("Capture all &screens (fullscreen)")
    {
        CheckOnClick = true
    };
    private readonly ToolStripMenuItem startWithWindows = new("Start with &Windows");
    private readonly NotifyIcon tray;
    private readonly HotkeyWindow hotkey;
    private readonly HotkeyWindow annotationHotkey;
    private readonly HotkeyWindow fullscreenHotkey;
    private readonly Icon appIcon;
    private string? saveFolder;
    private bool busy;

    internal TrayApplication(Icon icon)
    {
        appIcon = icon;
        menu.Items.Add("&Capture    PrintScreen", null, CaptureRequested);
        menu.Items.Add("Capture &and annotate    Shift+PrintScreen", null, AnnotationCaptureRequested);
        menu.Items.Add("&Fullscreen capture    Ctrl+Shift+PrintScreen", null, FullscreenCaptureRequested);
        menu.Items.Add(captureAllScreens);
        menu.Items.Add("Choose &folder…", null, (_, _) => ChooseFolderRequested());
        menu.Items.Add("&Open folder", null, (_, _) => OpenFolder());
        menu.Items.Add(new ToolStripSeparator());
        startWithWindows.Click += (_, _) => ToggleStartWithWindows();
        menu.Items.Add(startWithWindows);
        menu.Items.Add("E&xit", null, (_, _) => ExitThread());

        tray = new NotifyIcon
        {
            Icon = appIcon,
            Text = "Screenshotss — PrintScreen capture / Shift+PrintScreen annotate",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.DoubleClick += CaptureRequested;
        hotkey = new HotkeyWindow(() => CaptureRequested(this, EventArgs.Empty), Keys.PrintScreen);
        annotationHotkey = new HotkeyWindow(() => AnnotationCaptureRequested(this, EventArgs.Empty), Keys.Shift | Keys.PrintScreen);
        fullscreenHotkey = new HotkeyWindow(() => FullscreenCaptureRequested(this, EventArgs.Empty),
            Keys.Control | Keys.Shift | Keys.PrintScreen);
        Application.Idle += FirstLaunch;
    }

    private void FirstLaunch(object? sender, EventArgs e)
    {
        Application.Idle -= FirstLaunch;
        busy = true;
        try
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(StartupRegistration.KeyPath);
                using var preferences = Registry.CurrentUser.CreateSubKey(StartupRegistration.PreferencesKeyPath);
                StartupRegistration.Initialize(key, preferences, Application.ExecutablePath);
                startWithWindows.Checked = StartupRegistration.IsEnabled(key);
            }
            catch (Exception error)
            {
                ShowError($"Could not initialize the Start with Windows setting.\n\n{error.Message}");
            }

            try
            {
                saveFolder = AppSettings.Load(AppSettings.FilePath);
            }
            catch (Exception error)
            {
                ShowError($"Could not read the saved folder. Choose it again.\n\n{error.Message}");
            }

            if (saveFolder is null)
                ChooseFolder();
            if (!hotkey.Registered)
                ShowError("PrintScreen is already in use or could not be registered. "
                    + "You can still capture from the tray menu or by double-clicking the tray icon.");
            if (!annotationHotkey.Registered)
                ShowError("Shift+PrintScreen is already in use or could not be registered. "
                    + "Use Capture and annotate from the tray menu instead.");
            if (!fullscreenHotkey.Registered)
                ShowError("Ctrl+Shift+PrintScreen is already in use or could not be registered. "
                    + "Use Fullscreen capture from the tray menu instead.");
            tray.ShowBalloonTip(3000, "Screenshotss is ready",
                "PrintScreen: region. Shift+PrintScreen: annotate. Ctrl+Shift+PrintScreen: fullscreen.", ToolTipIcon.Info);
        }
        finally
        {
            busy = false;
        }
    }

    private async void CaptureRequested(object? sender, EventArgs e) => await CaptureAsync(annotate: false);

    private async void AnnotationCaptureRequested(object? sender, EventArgs e) => await CaptureAsync(annotate: true);

    private async void FullscreenCaptureRequested(object? sender, EventArgs e) => await CaptureAsync(annotate: false, fullscreen: true);

    private async Task CaptureAsync(bool annotate, bool fullscreen = false)
    {
        if (busy)
            return;
        busy = true;
        menu.Enabled = false;
        try
        {
            if (saveFolder is null && !ChooseFolder())
                return;
            if (!Directory.Exists(saveFolder))
            {
                ShowError("The save folder is unavailable. Use Choose folder from the tray menu to select it again.");
                return;
            }

            // Let the tray menu disappear before freezing the desktop.
            await Task.Delay(180);
            using var image = CaptureImage(annotate, fullscreen, captureAllScreens.Checked);
            if (image is null)
                return;

            if (annotate)
            {
                using var editor = new AnnotationEditor(image, SaveCapture, appIcon);
                editor.ShowDialog();
            }
            else
                await SaveCapture(image);
        }
        catch (Exception error)
        {
            ShowError($"Capture could not finish.\n\n{error.Message}");
        }
        finally
        {
            menu.Enabled = true;
            busy = false;
        }
    }

    internal static Bitmap? CaptureImage(bool annotate, bool fullscreen, bool allScreens = false)
    {
        var bounds = fullscreen && !allScreens
            ? Screen.FromPoint(Cursor.Position).Bounds
            : SystemInformation.VirtualScreen;
        if (fullscreen)
            return CaptureOverlay.Snapshot(bounds);
        using var desktop = CaptureOverlay.Snapshot(bounds);
        using var overlay = new CaptureOverlay(desktop, bounds, annotate);
        if (overlay.ShowDialog() != DialogResult.OK)
            return null;
        return desktop.Clone(overlay.Selection, PixelFormat.Format32bppArgb);
    }

    private async Task<bool> SaveCapture(Bitmap image)
    {
        try
        {
            if (!Directory.Exists(saveFolder) && !ChooseFolder())
                return false;
            var path = ScreenshotStore.Save(image, saveFolder!);
            try
            {
                await ScreenshotClipboard.CopyAsync(image, path);
            }
            catch (Exception error) when (error is ExternalException or TimeoutException)
            {
                ShowError($"Screenshot saved, but copying to the clipboard could not finish.\n\n{path}\n\n{error.Message}\n\n"
                    + "Use Open folder from the tray menu to find the image.");
                return true;
            }

            tray.ShowBalloonTip(2000, "Screenshot saved — path and image copied", path, ToolTipIcon.Info);
            return true;
        }
        catch (Exception error)
        {
            ShowError($"Could not save the screenshot.\n\n{error.Message}");
            return false;
        }
    }

    private void ToggleStartWithWindows()
    {
        if (busy)
            return;
        busy = true;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(StartupRegistration.KeyPath);
            using var preferences = Registry.CurrentUser.CreateSubKey(StartupRegistration.PreferencesKeyPath);
            var enabled = !StartupRegistration.IsEnabled(key);
            StartupRegistration.SetEnabled(key, preferences, enabled, Application.ExecutablePath);
            startWithWindows.Checked = enabled;
        }
        catch (Exception error)
        {
            ShowError($"Could not change the Start with Windows setting.\n\n{error.Message}");
        }
        finally
        {
            busy = false;
        }
    }

    private void ChooseFolderRequested()
    {
        if (busy)
            return;
        busy = true;
        try { ChooseFolder(); }
        finally { busy = false; }
    }

    private bool ChooseFolder()
    {
        try
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Choose where Screenshotss saves screenshots",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true,
                SelectedPath = saveFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };
            if (dialog.ShowDialog() != DialogResult.OK)
                return false;

            AppSettings.Save(AppSettings.FilePath, dialog.SelectedPath);
            saveFolder = Path.GetFullPath(dialog.SelectedPath);
            return true;
        }
        catch (Exception error)
        {
            ShowError($"Could not remember this folder.\n\n{error.Message}");
            return false;
        }
    }

    private void OpenFolder()
    {
        if (busy)
            return;
        busy = true;
        try
        {
            if (saveFolder is null && !ChooseFolder())
                return;
            if (!Directory.Exists(saveFolder))
                throw new DirectoryNotFoundException("The save folder is unavailable. Choose another folder.");
            using var process = Process.Start(new ProcessStartInfo(saveFolder!) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            ShowError($"Could not open the folder.\n\n{error.Message}");
        }
        finally
        {
            busy = false;
        }
    }

    private static void ShowError(string message) => MessageBox.Show(message, "Screenshotss",
        MessageBoxButtons.OK, MessageBoxIcon.Warning);

    protected override void ExitThreadCore()
    {
        tray.Visible = false;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Application.Idle -= FirstLaunch;
            hotkey.Dispose();
            annotationHotkey.Dispose();
            fullscreenHotkey.Dispose();
            tray.Dispose();
            menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
