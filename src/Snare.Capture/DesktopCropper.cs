using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Imaging;

namespace Snare.Capture;

/// <summary>
/// Cuts a selection out of the frozen frames, never the live screen. Only the selected area
/// is copied, so a small region from a 5120x2880 desktop stays cheap.
/// </summary>
public static class DesktopCropper
{
    public static PixelBuffer Crop(FrozenDesktop desktop, PixelRect selection)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        var rect = selection.Intersect(desktop.Bounds);
        if (rect.IsEmpty)
        {
            throw new ArgumentException($"Selection {selection} is outside the desktop {desktop.Bounds}.", nameof(selection));
        }

        var result = PixelBuffer.Filled(rect.Width, rect.Height, VirtualDesktopComposer.UncoveredArgb);

        foreach (var frozen in desktop.Monitors)
        {
            var monitorBounds = frozen.Monitor.Bounds;
            var overlap = rect.Intersect(monitorBounds);
            if (overlap.IsEmpty)
            {
                continue;
            }

            var frame = frozen.Frame;
            var rowBytes = overlap.Width * PixelBuffer.BytesPerPixel;
            for (var row = 0; row < overlap.Height; row++)
            {
                var sourceY = overlap.Y - monitorBounds.Y + row;
                var sourceX = overlap.X - monitorBounds.X;
                var targetY = overlap.Y - rect.Y + row;
                var targetX = overlap.X - rect.X;
                Buffer.BlockCopy(
                    frame.Pixels, (sourceY * frame.Stride) + (sourceX * PixelBuffer.BytesPerPixel),
                    result.Pixels, (targetY * result.Stride) + (targetX * PixelBuffer.BytesPerPixel),
                    rowBytes);
            }
        }

        return result;
    }
}
