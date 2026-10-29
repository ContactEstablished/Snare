using Snare.Core.Geometry;

namespace Snare.App.QuickBar;

/// <summary>Where the quick bar goes, in physical pixels: under the selection, inside the work area.</summary>
internal static class QuickBarPlacement
{
    public const int Gap = 12;

    /// <summary>
    /// Right-aligned under the selection. If there's no room below (a full-screen capture),
    /// it sits just inside the selection's bottom edge, still within the work area.
    /// </summary>
    public static PixelPoint Place(PixelSize bar, PixelRect selection, PixelRect workArea)
    {
        var x = Math.Max(workArea.X + Gap, Math.Min(selection.Right - bar.Width, workArea.Right - bar.Width - Gap));
        var y = selection.Bottom + Gap;
        if (y + bar.Height > workArea.Bottom - Gap)
        {
            y = Math.Max(workArea.Y + Gap, Math.Min(selection.Bottom, workArea.Bottom) - bar.Height - Gap);
        }

        return new PixelPoint(x, y);
    }
}
