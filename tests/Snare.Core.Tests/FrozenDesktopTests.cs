using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Imaging;

namespace Snare.Core.Tests;

public class FrozenDesktopTests
{
    private static readonly MonitorInfo Primary = new("primary", new PixelRect(0, 0, 4, 2), new PixelRect(0, 0, 4, 2), 96, true);
    private static readonly MonitorInfo Upper = new("upper", new PixelRect(1, -2, 2, 2), new PixelRect(1, -2, 2, 2), 144, false);

    [Fact]
    public void GetArgb_ReadsFromTheMonitorUnderThePoint()
    {
        var upperFrame = PixelBuffer.Filled(2, 2, 0xFF00FF00);
        upperFrame.Pixels[(1 * upperFrame.Stride) + 4 + 2] = 0xFF; // (1, 1) red channel
        var desktop = new FrozenDesktop(
            [new FrozenMonitor(Primary, PixelBuffer.Filled(4, 2, 0xFF112233)), new FrozenMonitor(Upper, upperFrame)],
            DateTimeOffset.UnixEpoch);

        Assert.Equal(0xFF112233u, desktop.GetArgb(new PixelPoint(3, 1)));
        Assert.Equal(0xFF00FF00u, desktop.GetArgb(new PixelPoint(1, -2)));
        Assert.Equal(0xFFFFFF00u, desktop.GetArgb(new PixelPoint(2, -1)));
    }

    [Fact]
    public void GetArgb_IsNullOffEveryMonitor()
    {
        var desktop = new FrozenDesktop(
            [new FrozenMonitor(Primary, PixelBuffer.Filled(4, 2, 0xFF000000)), new FrozenMonitor(Upper, PixelBuffer.Filled(2, 2, 0xFF000000))],
            DateTimeOffset.UnixEpoch);

        Assert.Null(desktop.GetArgb(new PixelPoint(0, -1)));
        Assert.Equal(new PixelRect(0, -2, 4, 4), desktop.Bounds);
    }

    [Fact]
    public void Constructor_RejectsAFrameThatDoesNotMatchItsMonitor()
    {
        Assert.Throws<ArgumentException>(() => new FrozenDesktop(
            [new FrozenMonitor(Primary, PixelBuffer.Filled(3, 2, 0))],
            DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void MonitorInfo_ScaleComesFromDpi()
    {
        Assert.Equal(1.5, Upper.Scale);
    }
}
