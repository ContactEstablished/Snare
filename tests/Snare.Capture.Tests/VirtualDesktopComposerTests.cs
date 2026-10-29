using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Imaging;

namespace Snare.Capture.Tests;

public class VirtualDesktopComposerTests
{
    private const uint Red = 0xFFFF0000;
    private const uint Blue = 0xFF0000FF;

    // Matt's layout scaled down: an 8x2 primary at (0, 0), a 4x2 monitor above it at (2, -2).
    private static FrozenDesktop StackedDesktop() => new(
        [
            Frozen("primary", new PixelRect(0, 0, 8, 2), Red, isPrimary: true),
            Frozen("upper", new PixelRect(2, -2, 4, 2), Blue, isPrimary: false),
        ],
        DateTimeOffset.UnixEpoch);

    [Fact]
    public void Compose_CoversTheBoundingBox()
    {
        var desktop = StackedDesktop();

        var composed = VirtualDesktopComposer.Compose(desktop);

        Assert.Equal(new PixelRect(0, -2, 8, 4), desktop.Bounds);
        Assert.Equal(8, composed.Width);
        Assert.Equal(4, composed.Height);
    }

    [Fact]
    public void Compose_PlacesEachMonitorAtItsOffset()
    {
        var composed = VirtualDesktopComposer.Compose(StackedDesktop());

        // Desktop (2, -2) is composed (2, 0); desktop (0, 0) is composed (0, 2).
        Assert.Equal(Blue, composed.GetArgb(2, 0));
        Assert.Equal(Blue, composed.GetArgb(5, 1));
        Assert.Equal(Red, composed.GetArgb(0, 2));
        Assert.Equal(Red, composed.GetArgb(7, 3));
    }

    [Fact]
    public void Compose_FillsUncoveredAreasWithOpaqueBlack()
    {
        var composed = VirtualDesktopComposer.Compose(StackedDesktop());

        Assert.Equal(VirtualDesktopComposer.UncoveredArgb, composed.GetArgb(0, 0));
        Assert.Equal(VirtualDesktopComposer.UncoveredArgb, composed.GetArgb(1, 1));
        Assert.Equal(VirtualDesktopComposer.UncoveredArgb, composed.GetArgb(6, 0));
        Assert.Equal(VirtualDesktopComposer.UncoveredArgb, composed.GetArgb(7, 1));
    }

    [Fact]
    public void Compose_KeepsRowOrderWithinAFrame()
    {
        var frame = PixelBuffer.Filled(2, 2, Red);
        frame.Pixels[(1 * frame.Stride) + 4] = 0xFF; // (1, 1) blue channel -> 0xFFFF00FF
        var desktop = new FrozenDesktop(
            [new FrozenMonitor(Monitor("only", new PixelRect(-2, -2, 2, 2), true), frame)],
            DateTimeOffset.UnixEpoch);

        var composed = VirtualDesktopComposer.Compose(desktop);

        Assert.Equal(0xFFFF00FFu, composed.GetArgb(1, 1));
        Assert.Equal(Red, composed.GetArgb(0, 1));
    }

    private static FrozenMonitor Frozen(string name, PixelRect bounds, uint argb, bool isPrimary) =>
        new(Monitor(name, bounds, isPrimary), PixelBuffer.Filled(bounds.Width, bounds.Height, argb));

    private static MonitorInfo Monitor(string name, PixelRect bounds, bool isPrimary) =>
        new(name, bounds, bounds, 96, isPrimary);
}
