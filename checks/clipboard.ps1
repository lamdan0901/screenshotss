$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -ReferencedAssemblies System.Windows.Forms, System.Windows.Forms.Primitives, System.Drawing.Common, System.Drawing.Primitives, System.Collections -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public sealed class ClipboardObserver : NativeWindow, IDisposable
{
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardViewer(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ChangeClipboardChain(IntPtr window, IntPtr next);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr w, IntPtr l);
    private IntPtr next;
    public readonly List<string> Copies = new List<string>();
    public ClipboardObserver()
    {
        CreateHandle(new CreateParams());
        next = SetClipboardViewer(Handle);
        Copies.Clear();
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x308) // WM_DRAWCLIPBOARD is synchronous, so even consecutive writes are observed.
        {
            string value = Clipboard.ContainsImage() ? "image" : Clipboard.GetText();
            if (Copies.Count == 0 || Copies[Copies.Count - 1] != value)
                Copies.Add(value);
            if (next != IntPtr.Zero) SendMessage(next, message.Msg, message.WParam, message.LParam);
        }
        else if (message.Msg == 0x30D)
        {
            if (message.WParam == next) next = message.LParam;
            else if (next != IntPtr.Zero) SendMessage(next, message.Msg, message.WParam, message.LParam);
        }
        base.WndProc(ref message);
    }
    public void Dispose()
    {
        ChangeClipboardChain(Handle, next);
        DestroyHandle();
    }
}
'@

$appDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Screenshotss/bin/Release/net10.0-windows10.0.22621.0'))
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $appDirectory 'Screenshotss.dll'))
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$folder = Join-Path $env:TEMP "Screenshotss-clipboard-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $folder | Out-Null
$icon = [Drawing.Icon]::ExtractAssociatedIcon((Join-Path $appDirectory 'Screenshotss.exe'))
$image = [Drawing.Bitmap]::new(12, 8)
$graphics = [Drawing.Graphics]::FromImage($image)
$graphics.Clear([Drawing.Color]::CornflowerBlue)
$graphics.Dispose()
$app = [Activator]::CreateInstance($assembly.GetType('Screenshotss.TrayApplication'), $flags,
    $null, [object[]]@($icon), $null)
$firstLaunch = $app.GetType().GetMethod('FirstLaunch', $flags).CreateDelegate([EventHandler], $app)
[Windows.Forms.Application]::remove_Idle($firstLaunch)
$observer = [ClipboardObserver]::new()
try {
    [Threading.SynchronizationContext]::SetSynchronizationContext(
        [Windows.Forms.WindowsFormsSynchronizationContext]::new())
    $app.GetType().GetField('saveFolder', $flags).SetValue($app, $folder)
    $pending = $app.GetType().GetMethod('SaveCapture', $flags).Invoke($app, [object[]]@($image))
    while (-not $pending.IsCompleted) {
        [Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 10
    }
    $saved = $pending.GetAwaiter().GetResult()
    if (-not $saved) { throw 'Capture must save successfully.' }
    $path = (Get-ChildItem -LiteralPath $folder -Filter '*.png').FullName
    if ($observer.Copies.Count -ne 2 -or $observer.Copies[0] -ne $path -or $observer.Copies[1] -ne 'image') {
        throw "Expected clipboard writes: saved path, then image. Observed: $($observer.Copies -join ', ')"
    }
    $image.Dispose()
    $copied = [Windows.Forms.Clipboard]::GetImage()
    try {
        if ($null -eq $copied -or $copied.Size -ne [Drawing.Size]::new(12, 8) -or
            $copied.GetPixel(4, 3).ToArgb() -ne [Drawing.Color]::CornflowerBlue.ToArgb()) {
            throw 'Clipboard must retain the captured dimensions and pixels after the source image is disposed.'
        }
    }
    finally { if ($null -ne $copied) { $copied.Dispose() } }
    Write-Output 'PASS capture saves PNG, copies path before image, and keeps image pixels after disposal.'
}
finally {
    $observer.Dispose()
    $app.Dispose()
    $image.Dispose()
    $icon.Dispose()
    Remove-Item -LiteralPath $folder -Recurse -Force
}
