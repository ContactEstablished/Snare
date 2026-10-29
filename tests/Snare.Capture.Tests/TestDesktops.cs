using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Imaging;

namespace Snare.Capture.Tests;

/// <summary>Small stand-ins for Matt's stacked monitors, with windows.</summary>
internal static class TestDesktops
{
    public const uint PrimaryColor = 0xFFFF0000;
    public const uint UpperColor = 0xFF0000FF;

    public static readonly WindowInfo Editor = new(1, "Notes - Editor", "notepad", new PixelRect(10, 10, 30, 20));
    public static readonly WindowInfo Browser = new(2, "Docs - Chrome", "chrome", new PixelRect(0, 0, 100, 40));
    public static readonly WindowInfo OffScreenEdge = new(3, "Hanging off", "app", new PixelRect(90, 30, 40, 20));

    /// <summary>A 100x40 primary at (0, 0) and a 60x40 monitor above it at (20, -40).</summary>
    public static FrozenDesktop Stacked(PixelPoint cursor = default) => new(
        [
            new FrozenMonitor(Monitor("primary", new PixelRect(0, 0, 100, 40), true), PixelBuffer.Filled(100, 40, PrimaryColor)),
            new FrozenMonitor(Monitor("upper", new PixelRect(20, -40, 60, 40), false), PixelBuffer.Filled(60, 40, UpperColor)),
        ],
        DateTimeOffset.UnixEpoch,
        [Editor, OffScreenEdge, Browser], // topmost first
        cursor);

    private static MonitorInfo Monitor(string name, PixelRect bounds, bool primary) => new(name, bounds, bounds, 96, primary);
}
