using Snare.App.QuickBar;
using Snare.Core.Geometry;

namespace Snare.App.Tests;

public class QuickBarPlacementTests
{
    private static readonly PixelSize Bar = new(500, 50);
    private static readonly PixelRect Work = new(0, 0, 5120, 1392); // Matt's primary minus the taskbar

    [Fact]
    public void SitsBelowTheSelection_RightAligned()
    {
        var at = QuickBarPlacement.Place(Bar, new PixelRect(1000, 200, 800, 400), Work);

        Assert.Equal(new PixelPoint(1800 - 500, 600 + QuickBarPlacement.Gap), at);
    }

    [Fact]
    public void FullScreen_TucksInsideTheBottomOfTheWorkArea()
    {
        var at = QuickBarPlacement.Place(Bar, new PixelRect(0, 0, 5120, 1440), Work);

        Assert.Equal(new PixelPoint(5120 - 500 - QuickBarPlacement.Gap, 1392 - 50 - QuickBarPlacement.Gap), at);
    }

    [Fact]
    public void NarrowSelectionAtTheLeftEdge_StaysOnScreen()
    {
        var at = QuickBarPlacement.Place(Bar, new PixelRect(0, 100, 40, 40), Work);

        Assert.Equal(QuickBarPlacement.Gap, at.X);
    }

    [Fact]
    public void UpperMonitor_UsesItsOwnNegativeWorkArea()
    {
        var upper = new PixelRect(856, -1440, 3440, 1440);

        var at = QuickBarPlacement.Place(Bar, new PixelRect(1000, -1000, 300, 300), upper);

        // Right-aligning would put it at x 800, left of the monitor; it is pushed in to 856 + gap.
        Assert.Equal(new PixelPoint(856 + QuickBarPlacement.Gap, -700 + QuickBarPlacement.Gap), at);
    }
}
