namespace Screenshotss;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var instance = new Mutex(true, @"Local\Screenshotss.Tray", out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("Screenshotss is already running. Use its tray icon, Ctrl+Shift+F9 or Ctrl+Shift+F10.",
                "Screenshotss", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                ?? throw new InvalidOperationException("Could not load the application icon.");
            using var app = new TrayApplication(icon);
            Application.Run(app);
        }
        catch (Exception error)
        {
            MessageBox.Show($"Screenshotss could not start.\n\n{error.Message}",
                "Screenshotss", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            instance.ReleaseMutex();
        }
    }
}
