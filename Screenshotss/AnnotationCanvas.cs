using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Screenshotss;

internal sealed class AnnotationCanvas : Control
{
    private readonly Bitmap image;
    private readonly List<AnnotationStroke> strokes = [];
    private AnnotationStroke? current;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal AnnotationTool Tool { get; set; } = AnnotationTool.Pen;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Color StrokeColor { get; set; } = Color.Red;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal float StrokeWidth { get; set; } = 3;
    internal bool CanUndo => current is not null || strokes.Count > 0;
    internal event EventHandler? Changed;

    internal AnnotationCanvas(Bitmap image)
    {
        this.image = image;
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(40, 40, 44);
        Cursor = Cursors.Cross;
        TabStop = true;
        AccessibleName = "Annotation canvas";
        AccessibleDescription = "Draw with the mouse. Use the toolbar to choose a tool, color and stroke width.";
        SetStyle(ControlStyles.Selectable, true);
    }

    internal static RectangleF ImageViewport(Size image, Size client)
    {
        var scale = Math.Min(1f, Math.Min((float)client.Width / image.Width, (float)client.Height / image.Height));
        var width = image.Width * scale;
        var height = image.Height * scale;
        return new RectangleF((client.Width - width) / 2, (client.Height - height) / 2, width, height);
    }

    internal static PointF ImagePoint(Point point, RectangleF viewport, Size image) => new(
        Math.Clamp((point.X - viewport.X) * image.Width / viewport.Width, 0, image.Width - 1),
        Math.Clamp((point.Y - viewport.Y) * image.Height / viewport.Height, 0, image.Height - 1));

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var viewport = ImageViewport(image.Size, ClientSize);
        if (viewport.Width <= 0 || viewport.Height <= 0)
            return;

        var state = e.Graphics.Save();
        try
        {
            e.Graphics.SetClip(viewport);
            e.Graphics.TranslateTransform(viewport.X, viewport.Y);
            e.Graphics.ScaleTransform(viewport.Width / image.Width, viewport.Height / image.Height);
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
            foreach (var stroke in strokes)
                AnnotationRenderer.Draw(e.Graphics, stroke);
            if (current is not null)
                AnnotationRenderer.Draw(e.Graphics, current);
        }
        finally
        {
            e.Graphics.Restore(state);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;
        var viewport = ImageViewport(image.Size, ClientSize);
        if (!viewport.Contains(e.Location))
            return;

        Focus();
        var point = ImagePoint(e.Location, viewport, image.Size);
        current = new AnnotationStroke(Tool, StrokeColor, StrokeWidth, [point]);
        if (Tool != AnnotationTool.Pen)
            current.Points.Add(point);
        Capture = true;
        Changed?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        UpdateStroke(e.Location);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
            return;
        UpdateStroke(e.Location);
        FinishStroke();
    }

    private void UpdateStroke(Point location)
    {
        if (current is null)
            return;
        var viewport = ImageViewport(image.Size, ClientSize);
        if (viewport.Width <= 0 || viewport.Height <= 0)
            return;
        var point = ImagePoint(location, viewport, image.Size);
        if (current.Tool == AnnotationTool.Pen)
        {
            if (current.Points[^1] != point)
                current.Points.Add(point);
        }
        else
            current.Points[^1] = point;
        Invalidate();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture)
            FinishStroke();
    }

    internal void FinishStroke()
    {
        if (current is null)
            return;
        if (current.Tool == AnnotationTool.Pen || current.Points[0] != current.Points[^1])
            strokes.Add(current);
        current = null;
        Capture = false;
        Changed?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    internal void Undo()
    {
        if (current is not null)
        {
            current = null;
            Capture = false;
        }
        else if (strokes.Count > 0)
            strokes.RemoveAt(strokes.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    internal Bitmap Render()
    {
        FinishStroke();
        return AnnotationRenderer.Render(image, strokes);
    }
}
