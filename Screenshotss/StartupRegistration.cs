using Microsoft.Win32;

namespace Screenshotss;

internal static class StartupRegistration
{
    internal const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string PreferencesKeyPath = @"Software\Screenshotss";
    private const string ValueName = "Screenshotss";
    private const string PreferenceName = "StartWithWindows";

    internal static bool IsEnabled(RegistryKey? key) =>
        !string.IsNullOrWhiteSpace(key?.GetValue(ValueName) as string);

    internal static void Initialize(RegistryKey key, RegistryKey preferences, string executablePath)
    {
        if (preferences.GetValue(PreferenceName) is null)
            SetEnabled(key, preferences, true, executablePath);
    }

    internal static void SetEnabled(RegistryKey key, RegistryKey preferences, bool enabled, string executablePath)
    {
        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            preferences.SetValue(PreferenceName, 0, RegistryValueKind.DWord);
            return;
        }

        if (!Path.IsPathFullyQualified(executablePath) || executablePath.Contains('"'))
            throw new ArgumentException("The startup executable must have an absolute path without quotes.");
        var command = $"\"{executablePath}\"";
        if (command.Length > 260)
            throw new InvalidOperationException("The app's path is too long for Windows startup. Move it to a shorter path and try again.");
        key.SetValue(ValueName, command, RegistryValueKind.String);
        preferences.SetValue(PreferenceName, 1, RegistryValueKind.DWord);
    }
}
