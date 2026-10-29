using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Screenshotss;

internal enum AnnotationTool { Pen, Line, Arrow }

internal sealed record AnnotationStroke(AnnotationTool Tool, Color Color, float Width, List<PointF> Points);

internal static class AnnotationRenderer
{
    internal static void Draw(Graphics graphics, AnnotationStroke stroke)
    {
        if (stroke.Points.Count == 0)
            return;

        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(stroke.Color, stroke.Width)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            if (stroke.Tool == AnnotationTool.Pen)
            {
                if (stroke.Points.Count == 1)
                {
                    var point = stroke.Points[0];
                    using var brush = new SolidBrush(stroke.Color);
                    graphics.FillEllipse(brush, point.X - stroke.Width / 2, point.Y - stroke.Width / 2,
                        stroke.Width, stroke.Width);
                }
                else
                    graphics.DrawLines(pen, stroke.Points.ToArray());
            }
            else if (stroke.Points.Count >= 2 && stroke.Points[0] != stroke.Points[^1])
            {
                if (stroke.Tool == AnnotationTool.Arrow)
                {
                    using var arrow = new AdjustableArrowCap(4, 5, isFilled: true);
                    pen.CustomEndCap = arrow;
                    graphics.DrawLine(pen, stroke.Points[0], stroke.Points[^1]);
                }
                else
                    graphics.DrawLine(pen, stroke.Points[0], stroke.Points[^1]);
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    internal static Bitmap Render(Bitmap original, IEnumerable<AnnotationStroke> strokes)
    {
        var image = original.Clone(new Rectangle(Point.Empty, original.Size), PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(image);
            foreach (var stroke in strokes)
                Draw(graphics, stroke);
            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }
}
