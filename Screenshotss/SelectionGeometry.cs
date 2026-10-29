namespace Screenshotss;

internal static class SelectionGeometry
{
    internal static Rectangle Between(Point start, Point end, Size desktop)
    {
        if (start.X == end.X || start.Y == end.Y)
            return Rectangle.Empty;

        var left = Math.Clamp(Math.Min(start.X, end.X), 0, desktop.Width);
        var top = Math.Clamp(Math.Min(start.Y, end.Y), 0, desktop.Height);
        // Cursor endpoints are pixels; rectangle right/bottom edges are exclusive.
        var right = Math.Min(Math.Max(start.X, end.X), desktop.Width - 1) + 1;
        var bottom = Math.Min(Math.Max(start.Y, end.Y), desktop.Height - 1) + 1;
        return right > left && bottom > top
            ? Rectangle.FromLTRB(left, top, right, bottom)
            : Rectangle.Empty;
    }
}
