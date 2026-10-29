using System.Drawing.Imaging;

namespace Screenshotss;

internal sealed class CaptureOverlay : Form
{
    private readonly Bitmap desktop;
    private readonly Rectangle screenBounds;
    private readonly bool annotate;
    private Point start;
    private Rectangle selection;
    private bool dragging;

    internal Rectangle Selection => selection;

    internal CaptureOverlay(Bitmap desktop, Rectangle screenBounds, bool annotate)
    {
        this.desktop = desktop;
        this.screenBounds = screenBounds;
        this.annotate = annotate;
        Text = "Screenshotss — drag to capture; Esc to cancel";
        AccessibleName = "Screenshot region selector";
        AccessibleDescription = "Drag the mouse to select an area. Press Escape or right-click to cancel.";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Bounds = screenBounds;
        ShowInTaskbar = false;
        TopMost = true;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        KeyPreview = true;
    }

    internal static Bitmap Snapshot(Rectangle bounds)
    {
        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // A spanning overlay must stay in physical pixels even on mixed-DPI displays.
        Bounds = screenBounds;
        Activate();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        e.Cancel = true;
        base.OnDpiChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImageUnscaled(desktop, Point.Empty);
        using var shade = new SolidBrush(Color.FromArgb(125, Color.Black));
        using var outside = new Region(ClientRectangle);
        if (!selection.IsEmpty)
            outside.Exclude(selection);
        e.Graphics.FillRegion(shade, outside);

        if (!selection.IsEmpty)
        {
            using var border = new Pen(Color.White, 2);
            e.Graphics.DrawRectangle(border, selection.X, selection.Y, selection.Width - 1, selection.Height - 1);
        }

        var monitor = Screen.FromPoint(Cursor.Position).Bounds;
        var anchor = new Point(monitor.Left - screenBounds.Left + 24, monitor.Top - screenBounds.Top + 24);
        var text = selection.IsEmpty
            ? "Drag to capture  ·  Esc / right-click to cancel"
            : $"{selection.Width} × {selection.Height} px  ·  Release to {(annotate ? "annotate" : "save")}  ·  Esc to cancel";
        var size = TextRenderer.MeasureText(text, Font);
        var label = new Rectangle(anchor, new Size(size.Width + 24, size.Height + 16));
        using var background = new SolidBrush(Color.FromArgb(240, 25, 25, 28));
        e.Graphics.FillRectangle(background, label);
        TextRenderer.DrawText(e.Graphics, text, Font, label, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Right)
        {
            Cancel();
            return;
        }
        if (e.Button != MouseButtons.Left)
            return;

        start = PointToClient(Cursor.Position);
        selection = Rectangle.Empty;
        dragging = true;
        Capture = true;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging)
            selection = SelectionGeometry.Between(start, PointToClient(Cursor.Position), desktop.Size);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!dragging || e.Button != MouseButtons.Left)
            return;

        selection = SelectionGeometry.Between(start, PointToClient(Cursor.Position), desktop.Size);
        dragging = false;
        Capture = false;
        if (selection.Width < 2 || selection.Height < 2)
        {
            selection = Rectangle.Empty;
            Invalidate();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            Cancel();
        }
        base.OnKeyDown(e);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (Visible && DialogResult == DialogResult.None)
            Cancel();
    }

    private void Cancel()
    {
        dragging = false;
        Capture = false;
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
