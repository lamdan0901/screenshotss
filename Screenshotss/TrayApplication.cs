using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Screenshotss;

internal sealed class TrayApplication : ApplicationContext
{
    private readonly ContextMenuStrip menu = new();
    private readonly NotifyIcon tray;
    private readonly HotkeyWindow hotkey;
    private readonly HotkeyWindow annotationHotkey;
    private readonly Icon appIcon;
    private string? saveFolder;
    private bool busy;

    internal TrayApplication(Icon icon)
    {
        appIcon = icon;
        menu.Items.Add("&Capture    Ctrl+Shift+F9", null, CaptureRequested);
        menu.Items.Add("Capture &and annotate    Ctrl+Shift+F10", null, AnnotationCaptureRequested);
        menu.Items.Add("Choose &folder…", null, (_, _) => ChooseFolderRequested());
        menu.Items.Add("&Open folder", null, (_, _) => OpenFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("E&xit", null, (_, _) => ExitThread());

        tray = new NotifyIcon
        {
            Icon = appIcon,
            Text = "Screenshotss — F9 capture / F10 annotate",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.DoubleClick += CaptureRequested;
        hotkey = new HotkeyWindow(() => CaptureRequested(this, EventArgs.Empty), Keys.F9);
        annotationHotkey = new HotkeyWindow(() => AnnotationCaptureRequested(this, EventArgs.Empty), Keys.F10);
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
                saveFolder = AppSettings.Load(AppSettings.FilePath);
            }
            catch (Exception error)
            {
                ShowError($"Could not read the saved folder. Choose it again.\n\n{error.Message}");
            }

            if (saveFolder is null)
                ChooseFolder();
            if (!hotkey.Registered)
                ShowError("Ctrl+Shift+F9 is already in use or could not be registered. "
                    + "You can still capture from the tray menu or by double-clicking the tray icon.");
            if (!annotationHotkey.Registered)
                ShowError("Ctrl+Shift+F10 is already in use or could not be registered. "
                    + "Use Capture and annotate from the tray menu instead.");
            tray.ShowBalloonTip(3000, "Screenshotss is ready",
                "Ctrl+Shift+F9: instant capture. Ctrl+Shift+F10: capture and annotate.", ToolTipIcon.Info);
        }
        finally
        {
            busy = false;
        }
    }

    private async void CaptureRequested(object? sender, EventArgs e) => await CaptureAsync(annotate: false);

    private async void AnnotationCaptureRequested(object? sender, EventArgs e) => await CaptureAsync(annotate: true);

    private async Task CaptureAsync(bool annotate)
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
            using var image = SelectRegion(annotate);
            if (image is null)
                return;

            if (annotate)
            {
                using var editor = new AnnotationEditor(image, SaveCapture, appIcon);
                editor.ShowDialog();
            }
            else
                SaveCapture(image);
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

    private static Bitmap? SelectRegion(bool annotate)
    {
        var bounds = SystemInformation.VirtualScreen;
        using var desktop = CaptureOverlay.Snapshot(bounds);
        using var overlay = new CaptureOverlay(desktop, bounds, annotate);
        if (overlay.ShowDialog() != DialogResult.OK)
            return null;
        return desktop.Clone(overlay.Selection, PixelFormat.Format32bppArgb);
    }

    private bool SaveCapture(Bitmap image)
    {
        try
        {
            if (!Directory.Exists(saveFolder) && !ChooseFolder())
                return false;
            var path = ScreenshotStore.Save(image, saveFolder!);
            try
            {
                var clipboard = new DataObject(DataFormats.UnicodeText, path);
                Clipboard.SetDataObject(clipboard, copy: true, retryTimes: 10, retryDelay: 75);
            }
            catch (ExternalException)
            {
                ShowError($"Screenshot saved, but the clipboard is busy.\n\n{path}\n\n"
                    + "Use Open folder from the tray menu to find the image.");
                return true;
            }

            tray.ShowBalloonTip(2000, "Screenshot saved — path copied", path, ToolTipIcon.Info);
            return true;
        }
        catch (Exception error)
        {
            ShowError($"Could not save the screenshot.\n\n{error.Message}");
            return false;
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
            tray.Dispose();
            menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
