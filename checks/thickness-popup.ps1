param([string]$OutputDirectory = (Join-Path $env:TEMP 'screenshotss-thickness-preview'))

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
[Windows.Forms.Application]::SetHighDpiMode([Windows.Forms.HighDpiMode]::PerMonitorV2) | Out-Null
[Windows.Forms.Application]::EnableVisualStyles()
[Windows.Forms.Application]::SetDefaultFont([Drawing.Font]::new('Segoe UI', 10))
$appDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Screenshotss/bin/Release/net10.0-windows'))
$assembly = [Reflection.Assembly]::LoadFile((Join-Path $appDirectory 'Screenshotss.dll'))
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$image = [Drawing.Bitmap]::new(760, 400)
$icon = [Drawing.Icon]::ExtractAssociatedIcon((Join-Path $appDirectory 'Screenshotss.exe'))
$save = [Func[Drawing.Bitmap,bool]] { param($capture) return $false }
$editor = [Activator]::CreateInstance($assembly.GetType('Screenshotss.AnnotationEditor'), $flags,
    $null, [object[]]@($image, $save, $icon), $null)
try {
    # Render only this test window; do not capture the user's desktop.
    $editor.Location = [Drawing.Point]::new(100, 100)
    $editor.StartPosition = [Windows.Forms.FormStartPosition]::Manual
    $editor.ShowInTaskbar = $false
    $editor.Opacity = 0
    $editor.Show()
    $toolbar = $editor.Controls | Where-Object { $_.GetType() -eq [Windows.Forms.ToolStrip] }
    $trigger = $toolbar.Items | Where-Object { $_.Tag -eq 'line-thickness' }
    $popup = $trigger.DropDown
    $popup.Opacity = 0
    $trigger.ShowDropDown()
    $layout = $popup.Items[0].Control.Controls[0]
    $slider = $layout.GetControlFromPosition(0, 0)
    $preview = $layout.GetControlFromPosition(1, 0)
    $value = $layout.GetControlFromPosition(2, 0)
    $canvas = $editor.GetType().GetField('canvas', $flags).GetValue($editor)
    $widthProperty = $canvas.GetType().GetProperty('StrokeWidth', $flags)
    if ($preview.Left -lt $slider.Right) { throw 'Slider and preview must be side by side.' }
    $triggerBottom = $toolbar.PointToScreen([Drawing.Point]::new($trigger.Bounds.Left, $trigger.Bounds.Bottom)).Y
    if ($popup.Top -lt $triggerBottom - 1) { throw 'Popup must open below the trigger.' }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $paintedWidths = @()
    foreach ($thickness in 1, 16, 32) {
        $slider.Value = $thickness
        if ($widthProperty.GetValue($canvas) -ne $thickness -or $value.Text -ne "$thickness px") {
            throw 'Changing the slider must immediately update drawing thickness and its label.'
        }
        $bitmap = [Drawing.Bitmap]::new($popup.Width, $popup.Height)
        $sample = [Drawing.Bitmap]::new($preview.Width, $preview.Height)
        try {
            $popup.DrawToBitmap($bitmap, [Drawing.Rectangle]::new(0, 0, $popup.Width, $popup.Height))
            $bitmap.Save((Join-Path $OutputDirectory "thickness-$thickness.png"))
            $preview.DrawToBitmap($sample, [Drawing.Rectangle]::new(0, 0, $preview.Width, $preview.Height))
            $painted = 0
            for ($y = 0; $y -lt $sample.Height; $y++) {
                $pixel = $sample.GetPixel([int]($sample.Width / 2), $y)
                if ($pixel.R -gt 200 -and $pixel.G -lt 220 -and $pixel.B -lt 220) { $painted++ }
            }
            $paintedWidths += $painted
        }
        finally { $bitmap.Dispose(); $sample.Dispose() }
    }
    if ($paintedWidths[2] -le $paintedWidths[0] + 20) { throw 'The preview must visibly grow with the slider.' }
    $trigger.HideDropDown()
    $trigger.ShowDropDown()
    if ($slider.Value -ne 32) { throw 'Reopening must preserve the selected thickness.' }
    Write-Output 'PASS popup placement, horizontal layout, live width/label, painted preview and retained selection.'
    Write-Output "Native previews: $OutputDirectory"
}
finally { $editor.Dispose(); $image.Dispose(); $icon.Dispose() }
