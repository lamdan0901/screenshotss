using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Clipboard = System.Windows.Forms.Clipboard;
using HistoryClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace Screenshotss;

internal static class ScreenshotClipboard
{
    internal static async Task CopyAsync(Bitmap image, string path)
    {
        var historyEnabled = HistoryClipboard.IsHistoryEnabled();
        var text = new DataObject(DataFormats.UnicodeText, path);
        Clipboard.SetDataObject(text, copy: true, retryTimes: 10, retryDelay: 75);
        if (historyEnabled)
            await WaitForPathAsync(path);

        var bitmap = new DataObject(DataFormats.Bitmap, image);
        Clipboard.SetDataObject(bitmap, copy: true, retryTimes: 10, retryDelay: 75);
    }

    private static async Task WaitForPathAsync(string path)
    {
        var elapsed = Stopwatch.StartNew();
        do
        {
            var history = await HistoryClipboard.GetHistoryItemsAsync();
            if (history.Status != ClipboardHistoryItemsResultStatus.Success)
                throw new ExternalException($"Windows clipboard history is unavailable: {history.Status}.");
            foreach (var item in history.Items)
                if (item.Content.Contains(StandardDataFormats.Text)
                    && await item.Content.GetTextAsync() == path)
                    return;

            // History reads clipboard changes asynchronously; replacing the path early loses it.
            await Task.Delay(25);
        } while (elapsed.Elapsed < TimeSpan.FromSeconds(5));

        throw new TimeoutException("Windows clipboard history did not retain the path. The path remains on the clipboard.");
    }
}
