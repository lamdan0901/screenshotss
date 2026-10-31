using System.Reflection;
using Screenshotss;
using Windows.ApplicationModel.DataTransfer;
using HistoryClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

internal static class ClipboardHistoryChecks
{
    internal static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            using var window = new Form { Opacity = 0, ShowInTaskbar = false };
            window.Shown += async (_, _) =>
            {
                try
                {
                    await VerifyAnnotationSaveAsync();
                    await VerifyAsync();
                }
                catch (Exception error) { failure = error; }
                finally { window.Close(); }
            };
            Application.Run(window);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
            throw new InvalidOperationException("Clipboard history check failed.", failure);
    }

    private static async Task VerifyAnnotationSaveAsync()
    {
        using var image = new Bitmap(12, 8);
        var pending = new TaskCompletionSource<bool>();
        var closed = new TaskCompletionSource();
        Bitmap? rendered = null;
        using var editor = new AnnotationEditor(image, capture =>
        {
            rendered = capture;
            return pending.Task;
        }, SystemIcons.Application) { Opacity = 0, ShowInTaskbar = false };
        editor.FormClosed += (_, _) => closed.TrySetResult();
        editor.Show();
        try
        {
            typeof(AnnotationEditor).GetMethod("SaveImage", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(editor, null);
            editor.Close();
            if (editor.IsDisposed || !editor.Visible || rendered?.Size != image.Size)
                throw new InvalidOperationException("The editor must keep the rendered image alive while clipboard history is pending.");
            pending.SetResult(true);
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Console.WriteLine("PASS annotation save waits for clipboard completion and prevents closing during the wait.");
        }
        finally { pending.TrySetResult(false); }
    }

    private static async Task VerifyAsync()
    {
        if (!HistoryClipboard.IsHistoryEnabled())
            throw new InvalidOperationException("Enable Windows clipboard history before running this opt-in check.");

        var folder = Path.Combine(Path.GetTempPath(), $"Screenshotss-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            using var image = new Bitmap(13, 9);
            using (var graphics = Graphics.FromImage(image))
                graphics.Clear(Color.CornflowerBlue);
            using var app = new TrayApplication(SystemIcons.Application);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            // Keep first-launch settings and startup registration out of this integration check.
            Application.Idle -= (EventHandler)typeof(TrayApplication).GetMethod("FirstLaunch", flags)!
                .CreateDelegate(typeof(EventHandler), app);
            typeof(TrayApplication).GetField("saveFolder", flags)!.SetValue(app, folder);
            var started = DateTimeOffset.Now;
            var result = typeof(TrayApplication).GetMethod("SaveCapture", flags)!.Invoke(app, [image]);
            var saved = await (Task<bool>)result!;
            if (!saved)
                throw new InvalidOperationException("Capture did not save.");
            var path = Directory.GetFiles(folder, "*.png").Single();
            DateTimeOffset? pathTime = null, imageTime = null;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            do
            {
                var history = await HistoryClipboard.GetHistoryItemsAsync();
                if (history.Status != ClipboardHistoryItemsResultStatus.Success)
                    throw new InvalidOperationException($"Clipboard history is unavailable: {history.Status}");
                foreach (var item in history.Items.Where(item => item.Timestamp >= started))
                {
                    if (item.Content.Contains(StandardDataFormats.Text)
                        && await item.Content.GetTextAsync() == path)
                        pathTime = item.Timestamp;
                    if (item.Content.Contains(StandardDataFormats.Bitmap))
                    {
                        using var source = await (await item.Content.GetBitmapAsync()).OpenReadAsync();
                        using var stream = source.AsStreamForRead();
                        using var copied = new Bitmap(stream);
                        if (copied.Size == image.Size
                            && copied.GetPixel(4, 3).ToArgb() == Color.CornflowerBlue.ToArgb())
                            imageTime = item.Timestamp;
                    }
                }
                if (pathTime.HasValue && imageTime > pathTime)
                {
                    Console.WriteLine("PASS Windows clipboard history retains separate path and image entries in order.");
                    return;
                }
                await Task.Delay(50);
            } while (DateTime.UtcNow < deadline);
            throw new InvalidOperationException($"Expected path then image in Windows history. Path retained: {pathTime.HasValue}; image retained: {imageTime.HasValue}.");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }
}
