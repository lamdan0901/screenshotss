using System.Drawing.Imaging;

namespace Screenshotss;

internal static class ScreenshotStore
{
    internal static string Save(Bitmap image, string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        if (!Path.IsPathFullyQualified(folder))
            throw new ArgumentException("Choose an absolute save folder.", nameof(folder));
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"The save folder no longer exists: {folder}");

        var filename = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.png";
        var path = Path.Combine(Path.GetFullPath(folder), filename);
        var temporary = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                image.Save(stream, ImageFormat.Png);
            File.Move(temporary, path);
            return path;
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
