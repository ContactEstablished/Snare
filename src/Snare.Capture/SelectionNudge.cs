using Snare.Core.Geometry;

namespace Snare.Capture;

/// <summary>Keyboard nudging for the capture overlay: arrow keys move 1 px, Shift+arrow moves 10 px.</summary>
public static class SelectionNudge
{
    public const int SmallStep = 1;
    public const int LargeStep = 10;

    /// <summary>
    /// Moves the selection by whole steps in the given direction and keeps it inside <paramref name="bounds"/>.
    /// A selection larger than the bounds is pinned to the bounds' top-left corner.
    /// </summary>
    public static PixelRect Move(PixelRect selection, int stepsX, int stepsY, bool large, PixelRect bounds)
    {
        var step = large ? LargeStep : SmallStep;
        var x = Clamp(selection.X + (stepsX * step), bounds.X, bounds.Right - selection.Width);
        var y = Clamp(selection.Y + (stepsY * step), bounds.Y, bounds.Bottom - selection.Height);
        return selection with { X = x, Y = y };
    }

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(value, max));
}
