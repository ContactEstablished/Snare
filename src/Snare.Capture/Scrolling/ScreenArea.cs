using Snare.Core.Geometry;
using Snare.Core.Imaging;
using Snare.Interop;

namespace Snare.Capture.Scrolling;

/// <summary>Live grabs of one screen area, without the mouse pointer, for a scrolling capture.</summary>
public static class ScreenArea
{
    public static Func<PixelBuffer> Grabber(PixelRect area) =>
        () => new PixelBuffer(area.Width, area.Height, ScreenGrabber.Grab(area.X, area.Y, area.Width, area.Height));
}
