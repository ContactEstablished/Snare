namespace Snare.Core.Geometry;

/// <summary>
/// A rectangle in physical screen pixels on the virtual desktop.
/// All capture geometry uses physical pixels; convert to WPF DIPs only for drawing overlay chrome.
/// </summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Builds a rectangle from two drag corners, in any order.</summary>
    public static PixelRect FromCorners(PixelPoint a, PixelPoint b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>Returns the overlap of two rectangles, or an empty rectangle when they don't overlap.</summary>
    public PixelRect Intersect(PixelRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);

        return right <= left || bottom <= top
            ? default
            : new PixelRect(left, top, right - left, bottom - top);
    }

    /// <summary>The smallest rectangle containing every non-empty input rectangle.</summary>
    public static PixelRect Union(IEnumerable<PixelRect> rects)
    {
        ArgumentNullException.ThrowIfNull(rects);

        int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
        foreach (var r in rects.Where(r => !r.IsEmpty))
        {
            left = Math.Min(left, r.X);
            top = Math.Min(top, r.Y);
            right = Math.Max(right, r.Right);
            bottom = Math.Max(bottom, r.Bottom);
        }

        return left == int.MaxValue ? default : new PixelRect(left, top, right - left, bottom - top);
    }

    public bool Contains(PixelPoint point) =>
        point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;
}
