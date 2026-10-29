using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Imaging;
using Snare.Interop;

namespace Snare.Capture;

/// <summary>Freezes every monitor with one GDI BitBlt each, in physical pixels.</summary>
public sealed class GdiScreenFreezer(TimeProvider clock) : IScreenFreezer
{
    public FrozenDesktop Freeze()
    {
        var capturedAt = clock.GetLocalNow();
        var frozen = DisplayMonitors.GetAll()
            .Select(m =>
            {
                var info = ToMonitorInfo(m);
                var b = info.Bounds;
                var pixels = ScreenGrabber.Grab(b.X, b.Y, b.Width, b.Height);
                return new FrozenMonitor(info, new PixelBuffer(b.Width, b.Height, pixels));
            })
            .ToList();

        return new FrozenDesktop(frozen, capturedAt);
    }

    internal static MonitorInfo ToMonitorInfo(DisplayMonitor m) => new(
        m.DeviceName,
        new PixelRect(m.Bounds.X, m.Bounds.Y, m.Bounds.Width, m.Bounds.Height),
        new PixelRect(m.WorkArea.X, m.WorkArea.Y, m.WorkArea.Width, m.WorkArea.Height),
        (int)m.Dpi,
        m.IsPrimary);
}
