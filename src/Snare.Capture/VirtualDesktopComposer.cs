using Snare.Core.Captures;
using Snare.Core.Imaging;

namespace Snare.Capture;

/// <summary>
/// Combines per-monitor frames into one bitmap over the desktop's bounding box, so a region
/// can span monitors. Areas no monitor covers are opaque black.
/// </summary>
public static class VirtualDesktopComposer
{
    public const uint UncoveredArgb = 0xFF000000;

    public static PixelBuffer Compose(FrozenDesktop desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        var bounds = desktop.Bounds;
        var result = PixelBuffer.Filled(bounds.Width, bounds.Height, UncoveredArgb);

        foreach (var frozen in desktop.Monitors)
        {
            var frame = frozen.Frame;
            var offsetX = frozen.Monitor.Bounds.X - bounds.X;
            var offsetY = frozen.Monitor.Bounds.Y - bounds.Y;

            for (var row = 0; row < frame.Height; row++)
            {
                Buffer.BlockCopy(
                    frame.Pixels, row * frame.Stride,
                    result.Pixels, ((offsetY + row) * result.Stride) + (offsetX * PixelBuffer.BytesPerPixel),
                    frame.Stride);
            }
        }

        return result;
    }
}
