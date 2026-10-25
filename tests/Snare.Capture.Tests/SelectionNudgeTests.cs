using Snare.Core.Geometry;

namespace Snare.Capture.Tests;

public class SelectionNudgeTests
{
    private static readonly PixelRect Desktop = new(0, 0, 1920, 1080);

    [Fact]
    public void ArrowKey_MovesOnePixel()
    {
        var moved = SelectionNudge.Move(new PixelRect(100, 100, 50, 50), 1, 0, large: false, Desktop);

        Assert.Equal(new PixelRect(101, 100, 50, 50), moved);
    }

    [Fact]
    public void ShiftArrow_MovesTenPixels()
    {
        var moved = SelectionNudge.Move(new PixelRect(100, 100, 50, 50), 0, -1, large: true, Desktop);

        Assert.Equal(new PixelRect(100, 90, 50, 50), moved);
    }

    [Fact]
    public void Move_StopsAtTheDesktopEdge()
    {
        var moved = SelectionNudge.Move(new PixelRect(1865, 5, 50, 50), 1, -1, large: true, Desktop);

        Assert.Equal(new PixelRect(1870, 0, 50, 50), moved);
    }

    [Fact]
    public void Move_WorksOnNegativeVirtualDesktopCoordinates()
    {
        // A monitor to the left of the primary has negative X.
        var leftMonitor = new PixelRect(-2560, 0, 2560, 1440);

        var moved = SelectionNudge.Move(new PixelRect(-2555, 10, 100, 100), -1, 0, large: true, leftMonitor);

        Assert.Equal(new PixelRect(-2560, 10, 100, 100), moved);
    }
}
