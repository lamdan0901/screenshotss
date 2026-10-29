using System.Drawing.Drawing2D;

namespace Screenshotss;

internal sealed class AnnotationEditor : Form
{
    private readonly AnnotationCanvas canvas;
    private readonly Func<Bitmap, bool> saveCapture;
    private readonly ToolStrip toolbar = new()
    {
        GripStyle = ToolStripGripStyle.Hidden,
        Padding = new Padding(10, 8, 10, 8),
        AutoSize = false,
        Height = 56,
        BackColor = Color.White,
        RenderMode = ToolStripRenderMode.System,
        LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
        CanOverflow = false
    };
    private readonly ToolStripButton undo = new("Undo (Ctrl+Z)") { Enabled = false };
    private readonly ToolStripButton color = new("Color")
    {
        AccessibleName = "Annotation color",
        ToolTipText = "Choose annotation color (currently #FF0000)"
    };
    private readonly List<Image> toolbarImages = [];
    private readonly ToolStripDropDown widthPopup = new() { Padding = Padding.Empty };
    private bool saving;

    internal AnnotationEditor(Bitmap image, Func<Bitmap, bool> saveCapture, Icon icon)
    {
        this.saveCapture = saveCapture;
        Text = "Screenshotss — Annotate";
        AccessibleName = "Screenshot annotation editor";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(Math.Clamp(image.Width, 760, 1200), Math.Clamp(image.Height + 100, 480, 850));
        MinimumSize = new Size(700, 400);
        Icon = icon;
        toolbar.Font = Font;

        canvas = new AnnotationCanvas(image) { Dock = DockStyle.Fill };
        canvas.Changed += (_, _) => undo.Enabled = canvas.CanUndo;
        var tools = new List<ToolStripButton>();
        foreach (var tool in Enum.GetValues<AnnotationTool>())
        {
            var button = new ToolStripButton(tool.ToString())
            {
                Checked = tool == AnnotationTool.Pen,
                Tag = tool.ToString().ToLowerInvariant(),
                AccessibleName = $"{tool} tool",
                ToolTipText = $"Draw with {tool.ToString().ToLowerInvariant()}"
            };
            button.Click += (_, _) =>
            {
                canvas.FinishStroke();
                canvas.Tool = tool;
                foreach (var item in tools)
                    item.Checked = item == button;
            };
            tools.Add(button);
            toolbar.Items.Add(button);
        }

        toolbar.Items.Add(new ToolStripSeparator { AutoSize = false, Size = new Size(12, 36) });
        color.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = canvas.StrokeColor, FullOpen = true };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            canvas.StrokeColor = dialog.Color;
            UpdateColorSwatch();
            color.ToolTipText = $"Choose annotation color (currently #{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2})";
        };
        toolbar.Items.Add(color);
        var width = new TrackBar
        {
            Minimum = 1,
            Maximum = 32,
            Value = 3,
            TickStyle = TickStyle.None,
            AutoSize = false,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Height = 28,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 12, 0),
            AccessibleName = "Annotation width in image pixels"
        };
        var preview = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            AccessibleName = "Live stroke thickness preview"
        };
        var widthValue = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Text = "3 px" };
        var widthLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        widthLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        widthLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        widthLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
        widthLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        widthLayout.Controls.Add(width, 0, 0);
        widthLayout.Controls.Add(preview, 1, 0);
        widthLayout.Controls.Add(widthValue, 2, 0);
        var widthContent = new UserControl
        {
            AutoScaleMode = AutoScaleMode.Dpi,
            AutoScaleDimensions = new SizeF(96, 96),
            Size = new Size(360, 64),
            Padding = new Padding(10),
            BackColor = Color.White,
            Font = Font
        };
        widthContent.Controls.Add(widthLayout);
        widthPopup.Items.Add(new ToolStripControlHost(widthContent)
        {
            AutoSize = false, Size = widthContent.Size, Margin = Padding.Empty, Padding = Padding.Empty
        });
        var thickness = new ToolStripDropDownButton("Stroke thickness")
        {
            Tag = "line-thickness",
            AccessibleName = "Stroke thickness",
            ToolTipText = "Stroke thickness: 3 px",
            ShowDropDownArrow = false,
            DropDownDirection = ToolStripDropDownDirection.BelowRight,
            DropDown = widthPopup
        };
        preview.Paint += (_, e) =>
        {
            var scale = preview.DeviceDpi / 96f;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(canvas.StrokeColor, canvas.StrokeWidth * scale)
            {
                StartCap = LineCap.Round, EndCap = LineCap.Round
            };
            e.Graphics.DrawLine(pen, 18 * scale, preview.Height / 2f, preview.Width - 18 * scale, preview.Height / 2f);
        };
        width.ValueChanged += (_, _) =>
        {
            canvas.StrokeWidth = width.Value;
            widthValue.Text = $"{width.Value} px";
            thickness.ToolTipText = $"Stroke thickness: {width.Value} px";
            preview.Invalidate();
        };
        thickness.DropDownOpening += (_, _) =>
        {
            canvas.FinishStroke();
            width.Value = (int)canvas.StrokeWidth;
            preview.Invalidate();
        };
        widthPopup.Opened += (_, _) => width.Focus();
        toolbar.Items.Add(thickness);
        toolbar.Items.Add(new ToolStripSeparator { AutoSize = false, Size = new Size(12, 36) });
        undo.Click += (_, _) => canvas.Undo();
        undo.Tag = "undo";
        toolbar.Items.Add(undo);

        var save = new ToolStripButton("Save")
        {
            Tag = "save",
            AccessibleName = "Save screenshot and copy full path",
            ToolTipText = "Save screenshot and copy full path (Ctrl+S)"
        };
        save.Click += (_, _) => SaveImage();
        toolbar.Items.Add(save);
        var discard = new ToolStripButton("Discard")
        {
            Tag = "discard",
            AccessibleName = "Discard unsaved screenshot"
        };
        discard.Click += (_, _) => Close();
        toolbar.Items.Add(discard);

        foreach (ToolStripItem button in toolbar.Items)
        {
            if (button is ToolStripSeparator)
                continue;
            button.AutoSize = false;
            button.Size = new Size(38, 36);
            button.Margin = new Padding(2);
            button.ImageScaling = ToolStripItemImageScaling.None;
            button.DisplayStyle = ToolStripItemDisplayStyle.Image;
        }
        RefreshToolbarImages();

        var status = new StatusStrip();
        status.Items.Add($"{image.Width} × {image.Height} px  ·  Drag to draw  ·  Save: Ctrl+S  ·  Undo: Ctrl+Z");
        Controls.Add(canvas);
        Controls.Add(toolbar);
        Controls.Add(status);
    }

    private Image LoadToolbarIcon(string name)
    {
        using var stream = typeof(AnnotationEditor).Assembly.GetManifestResourceStream($"Screenshotss.Assets.Toolbar.{name}.png")
            ?? throw new InvalidOperationException($"Missing toolbar icon: {name}");
        using var source = Image.FromStream(stream);
        var side = LogicalToDeviceUnits(24);
        var image = new Bitmap(side, side);
        using var graphics = Graphics.FromImage(image);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.DrawImage(source, new Rectangle(0, 0, side, side), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
        return image;
    }

    private void ReplaceToolbarImage(ToolStripItem button, Image image)
    {
        var previous = button.Image;
        button.Image = image;
        toolbarImages.Add(image);
        if (previous is not null)
        {
            toolbarImages.Remove(previous);
            previous.Dispose();
        }
    }

    private void UpdateColorSwatch()
    {
        var side = LogicalToDeviceUnits(24);
        var inset = LogicalToDeviceUnits(4);
        var image = new Bitmap(side, side);
        using var graphics = Graphics.FromImage(image);
        using var brush = new SolidBrush(canvas.StrokeColor);
        var bounds = new Rectangle(inset, inset, side - inset * 2 - 1, side - inset * 2 - 1);
        graphics.FillRectangle(brush, bounds);
        using var border = new Pen(Color.Gray);
        graphics.DrawRectangle(border, bounds);
        ReplaceToolbarImage(color, image);
    }

    private void RefreshToolbarImages()
    {
        foreach (ToolStripItem button in toolbar.Items)
            if (button.Tag is string name)
                ReplaceToolbarImage(button, LoadToolbarIcon(name));
        UpdateColorSwatch();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RefreshToolbarImages();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        RefreshToolbarImages();
    }

    private void SaveImage()
    {
        if (saving)
            return;
        saving = true;
        toolbar.Enabled = false;
        try
        {
            using var rendered = canvas.Render();
            if (!saveCapture(rendered))
                return;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception error)
        {
            MessageBox.Show(this, $"Could not save the screenshot. Your capture is still open.\n\n{error.Message}",
                "Screenshotss", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            saving = false;
            toolbar.Enabled = true;
        }
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.S))
        {
            SaveImage();
            return true;
        }
        if (keyData == (Keys.Control | Keys.Z))
        {
            canvas.Undo();
            return true;
        }
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            widthPopup.Dispose();
            foreach (var image in toolbarImages)
                image.Dispose();
            toolbarImages.Clear();
        }
    }
}
