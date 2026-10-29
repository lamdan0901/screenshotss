using System.Text.Json;

namespace Screenshotss;

internal static class AppSettings
{
    internal static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Screenshotss", "settings.json");

    internal static string? Load(string path)
    {
        if (!File.Exists(path))
            return null;

        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path));
        if (settings is null || string.IsNullOrWhiteSpace(settings.SaveFolder)
            || !Path.IsPathFullyQualified(settings.SaveFolder))
            throw new InvalidDataException("The saved folder setting is invalid. Choose the folder again.");

        return Path.GetFullPath(settings.SaveFolder);
    }

    internal static void Save(string path, string folder)
    {
        if (!Path.IsPathFullyQualified(folder) || !Directory.Exists(folder))
            throw new DirectoryNotFoundException("Choose an existing absolute folder.");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Settings(Path.GetFullPath(folder))));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private sealed record Settings(string SaveFolder);
}
