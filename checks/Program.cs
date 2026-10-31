using System.Drawing.Imaging;
using System.Text.Json;
using Microsoft.Win32;
using Screenshotss;

if (args.Contains("--clipboard-history"))
{
    ClipboardHistoryChecks.Run();
    return;
}

var root = Path.Combine(Path.GetTempPath(), $"Screenshotss-checks-{Guid.NewGuid():N}");
Directory.CreateDirectory(root);
var passed = 0;

void Check(bool condition, string description)
{
    if (!condition)
        throw new InvalidOperationException(description);
    Console.WriteLine($"PASS {description}");
    passed++;
}

void Throws<T>(Action action, string description) where T : Exception
{
    try { action(); }
    catch (T) { Check(true, description); return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}: {description}");
}

try
{
    var startupKeyPath = $@"Software\Screenshotss-checks-{Guid.NewGuid():N}";
    try
    {
        using var key = Registry.CurrentUser.CreateSubKey(startupKeyPath);
        using var preferences = Registry.CurrentUser.CreateSubKey(startupKeyPath + "-preferences");
        key.SetValue("Unrelated", "keep");
        Check(!StartupRegistration.IsEnabled(null) && !StartupRegistration.IsEnabled(key),
            "Startup is disabled when no registration exists");
        var executablePath = Path.Combine(root, "Tiếng Việt", "Screenshotss with spaces.exe");
        StartupRegistration.Initialize(key, preferences, executablePath);
        Check(StartupRegistration.IsEnabled(key) && (string?)key.GetValue("Screenshotss") == $"\"{executablePath}\""
            && key.GetValueKind("Screenshotss") == RegistryValueKind.String
            && (int?)preferences.GetValue("StartWithWindows") == 1,
            "First launch enables startup by default with a quoted path including spaces and Unicode");
        Throws<ArgumentException>(() => StartupRegistration.SetEnabled(key, preferences, true, "relative.exe"),
            "Startup rejects relative executable paths");
        Throws<ArgumentException>(() => StartupRegistration.SetEnabled(key, preferences, true, executablePath + '\"'),
            "Startup rejects quotes inside executable paths");
        Throws<InvalidOperationException>(() => StartupRegistration.SetEnabled(key, preferences, true, @"C:\" + new string('a', 260) + ".exe"),
            "Startup rejects commands longer than Windows allows");
        Check((string?)key.GetValue("Screenshotss") == $"\"{executablePath}\"",
            "Invalid startup paths preserve the previous registration");
        StartupRegistration.SetEnabled(key, preferences, false, executablePath);
        StartupRegistration.SetEnabled(key, preferences, false, executablePath);
        StartupRegistration.Initialize(key, preferences, executablePath);
        Check(!StartupRegistration.IsEnabled(key) && key.GetValue("Screenshotss") is null
            && (string?)key.GetValue("Unrelated") == "keep"
            && (int?)preferences.GetValue("StartWithWindows") == 0,
            "Disabling startup preserves unrelated entries and remains disabled on later launches");
        StartupRegistration.SetEnabled(key, preferences, true, executablePath);
        StartupRegistration.Initialize(key, preferences, executablePath);
        Check(StartupRegistration.IsEnabled(key), "Startup can be enabled again after opting out");
    }
    finally
    {
        Registry.CurrentUser.DeleteSubKey(startupKeyPath, throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKey(startupKeyPath + "-preferences", throwOnMissingSubKey: false);
    }

    foreach (var name in new[] { "pen", "line", "arrow", "line-thickness", "undo", "save", "discard" })
    {
        using var stream = typeof(AnnotationEditor).Assembly.GetManifestResourceStream($"Screenshotss.Assets.Toolbar.{name}.png");
        Check(stream is not null, $"Toolbar icon {name} is embedded in the application");
        using var toolbarIcon = Image.FromStream(stream!);
        Check(toolbarIcon.Width > 0 && toolbarIcon.Height > 0, $"Toolbar icon {name} decodes as an image");
    }

    using (var fullscreen = TrayApplication.CaptureImage(annotate: false, fullscreen: true))
        Check(fullscreen is not null && fullscreen.Size == Screen.FromPoint(Cursor.Position).Bounds.Size,
            "Fullscreen capture defaults to the monitor under the cursor without opening a region selector");

    using (var fullscreen = TrayApplication.CaptureImage(annotate: false, fullscreen: true, allScreens: true))
        Check(fullscreen is not null && fullscreen.Size == SystemInformation.VirtualScreen.Size,
            "All-screens fullscreen capture returns the entire virtual desktop without opening a region selector");

    using var image = new Bitmap(12, 8, PixelFormat.Format32bppArgb);
    using (var graphics = Graphics.FromImage(image))
        graphics.Clear(Color.CornflowerBlue);

    var folder = Path.Combine(root, "Screenshots with spaces", "Tiếng Việt");
    Directory.CreateDirectory(folder);
    var path = ScreenshotStore.Save(image, folder);
    Check(Path.IsPathFullyQualified(path) && Path.GetDirectoryName(path) == folder,
        "Saved path is absolute and stays in the selected Unicode folder");
    using (var saved = new Bitmap(path))
    {
        Check(saved.Size == new Size(12, 8) && saved.GetPixel(4, 3).ToArgb() == Color.CornflowerBlue.ToArgb(),
            "PNG preserves dimensions and pixels");
        Check(saved.RawFormat.Guid == ImageFormat.Png.Guid, "Saved file really is a PNG");
    }
    var original = File.ReadAllBytes(path);
    var paths = Enumerable.Range(0, 30).Select(_ => ScreenshotStore.Save(image, folder)).ToArray();
    Check(paths.Distinct().Count() == 30 && !paths.Contains(path), "Rapid captures have distinct filenames");
    Check(File.ReadAllBytes(path).SequenceEqual(original), "Later captures never overwrite the first image");
    Throws<DirectoryNotFoundException>(() => ScreenshotStore.Save(image, Path.Combine(root, "missing")),
        "A removed save folder fails without silently switching destinations");
    Throws<ArgumentException>(() => ScreenshotStore.Save(image, "relative-folder"),
        "Relative destinations are rejected");

    var settingsPath = Path.Combine(root, "settings", "settings.json");
    Check(AppSettings.Load(settingsPath) is null, "Missing settings require a folder choice");
    AppSettings.Save(settingsPath, folder);
    Check(AppSettings.Load(settingsPath) == folder, "Folder selection survives settings reload");
    var otherFolder = Path.Combine(root, "other");
    Directory.CreateDirectory(otherFolder);
    AppSettings.Save(settingsPath, otherFolder);
    Check(AppSettings.Load(settingsPath) == otherFolder, "Changing folders replaces saved settings");
    var malformed = "{ broken json";
    File.WriteAllText(settingsPath, malformed);
    Throws<JsonException>(() => AppSettings.Load(settingsPath), "Malformed settings are reported");
    Check(File.ReadAllText(settingsPath) == malformed, "Reading malformed settings preserves the original file");
    File.WriteAllText(settingsPath, "{\"SaveFolder\":\"relative\"}");
    Throws<InvalidDataException>(() => AppSettings.Load(settingsPath), "Invalid stored destinations are rejected");

    var desktop = new Size(3840, 2160);
    Check(SelectionGeometry.Between(new Point(0, 0), new Point(3839, 2159), desktop) == new Rectangle(0, 0, 3840, 2160),
        "Corner-to-corner drag includes the final row and column");
    Check(SelectionGeometry.Between(new Point(3839, 2159), new Point(0, 0), desktop) == new Rectangle(0, 0, 3840, 2160),
        "Reverse corner-to-corner drag includes the entire desktop");
    Check(SelectionGeometry.Between(new Point(500, 400), new Point(100, 80), desktop) == new Rectangle(100, 80, 401, 321),
        "Reverse drag normalizes correctly");
    Check(SelectionGeometry.Between(new Point(-30, -40), new Point(4000, 2300), desktop) == new Rectangle(0, 0, 3840, 2160),
        "Selection clips to the desktop bitmap");
    Check(SelectionGeometry.Between(new Point(100, 80), new Point(100, 80), desktop).IsEmpty,
        "A click without dragging has no selection");
    Check(SelectionGeometry.Between(new Point(100, 80), new Point(100, 300), desktop).IsEmpty,
        "A vertical line without area has no selection");
    Check(SelectionGeometry.Between(new Point(4000, 100), new Point(4100, 300), desktop).IsEmpty,
        "A drag entirely outside the desktop has no selection");
    var virtualOrigin = new Point(-1920, -1080);
    var screenStart = new Point(-1700, -900);
    var screenEnd = new Point(200, 100);
    Check(SelectionGeometry.Between(
            new Point(screenStart.X - virtualOrigin.X, screenStart.Y - virtualOrigin.Y),
            new Point(screenEnd.X - virtualOrigin.X, screenEnd.Y - virtualOrigin.Y), desktop)
        == new Rectangle(220, 180, 1901, 1001), "Selection spans negative monitor coordinates");

    var viewport = AnnotationCanvas.ImageViewport(new Size(1600, 800), new Size(800, 600));
    Check(viewport == new RectangleF(0, 100, 800, 400), "Annotation preview fits and letterboxes large captures");
    Check(AnnotationCanvas.ImagePoint(new Point(400, 300), viewport, new Size(1600, 800)) == new PointF(800, 400),
        "Preview coordinates map back to original image pixels");
    Check(AnnotationCanvas.ImagePoint(new Point(900, -20), viewport, new Size(1600, 800)) == new PointF(1599, 0),
        "Drawing outside the preview clamps to image boundaries");
    Check(AnnotationCanvas.ImageViewport(new Size(200, 100), new Size(600, 400)) == new RectangleF(200, 150, 200, 100),
        "Small captures stay at original size in the preview");

    using var originalImage = new Bitmap(200, 120, PixelFormat.Format32bppArgb);
    using (var graphics = Graphics.FromImage(originalImage))
        graphics.Clear(Color.White);
    var annotations = new List<AnnotationStroke>
    {
        new(AnnotationTool.Pen, Color.Red, 6, [new(20, 20), new(30, 25), new(40, 20)]),
        new(AnnotationTool.Line, Color.Blue, 8, [new(20, 50), new(100, 50)]),
        new(AnnotationTool.Arrow, Color.Green, 4, [new(20, 90), new(170, 90)]),
        new(AnnotationTool.Pen, Color.Purple, 6, [new(150, 20)])
    };
    using var annotated = AnnotationRenderer.Render(originalImage, annotations);
    Check(annotated.Size == new Size(200, 120), "Annotated export preserves original capture dimensions");
    Check(annotated.GetPixel(30, 25).ToArgb() == Color.Red.ToArgb(), "Freehand pen keeps its path and color");
    Check(annotated.GetPixel(50, 53).ToArgb() == Color.Blue.ToArgb()
        && annotated.GetPixel(50, 57).ToArgb() == Color.White.ToArgb(), "Line export respects the chosen stroke width");
    Check(annotated.GetPixel(150, 20).ToArgb() == Color.Purple.ToArgb(), "A single pen click draws a dot");
    Check(annotated.GetPixel(80, 90).ToArgb() == Color.Green.ToArgb()
        && Enumerable.Range(145, 24).Any(x => annotated.GetPixel(x, 85).ToArgb() == Color.Green.ToArgb()),
        "Arrow has a shaft and an arrowhead pointing toward its endpoint");
    Check(originalImage.GetPixel(30, 25).ToArgb() == Color.White.ToArgb(), "Annotations preserve the original capture for undo");
    annotations.RemoveAt(annotations.Count - 1);
    using var undone = AnnotationRenderer.Render(originalImage, annotations);
    Check(undone.GetPixel(150, 20).ToArgb() == Color.White.ToArgb(), "Removing the last annotation restores its original pixels");
    var annotatedPath = ScreenshotStore.Save(annotated, folder);
    using (var reloaded = new Bitmap(annotatedPath))
        Check(reloaded.Size == new Size(200, 120) && reloaded.GetPixel(30, 25).ToArgb() == Color.Red.ToArgb(),
            "Saving an annotated PNG uses the shared save pipeline and retains annotations");

    Console.WriteLine($"{passed} checks passed.");
}
finally
{
    Directory.Delete(root, recursive: true);
}
