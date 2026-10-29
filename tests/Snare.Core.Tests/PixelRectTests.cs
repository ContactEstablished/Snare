using Snare.Core.Geometry;

namespace Snare.Core.Tests;

public class PixelRectTests
{
    [Fact]
    public void FromCorners_NormalizesADragInAnyDirection()
    {
        var rect = PixelRect.FromCorners(new PixelPoint(300, 400), new PixelPoint(100, 150));

        Assert.Equal(new PixelRect(100, 150, 200, 250), rect);
    }

    [Fact]
    public void Intersect_ReturnsTheOverlap()
    {
        var a = new PixelRect(0, 0, 100, 100);
        var b = new PixelRect(50, 60, 100, 100);

        Assert.Equal(new PixelRect(50, 60, 50, 40), a.Intersect(b));
    }

    [Fact]
    public void Intersect_OfSeparateRectsIsEmpty()
    {
        var a = new PixelRect(0, 0, 10, 10);
        var b = new PixelRect(20, 20, 10, 10);

        Assert.True(a.Intersect(b).IsEmpty);
    }

    [Fact]
    public void Contains_ExcludesTheRightAndBottomEdges()
    {
        var rect = new PixelRect(0, 0, 10, 10);

        Assert.True(rect.Contains(new PixelPoint(9, 9)));
        Assert.False(rect.Contains(new PixelPoint(10, 5)));
    }

    [Fact]
    public void Union_CoversMattsStackedMonitors()
    {
        var primary = new PixelRect(0, 0, 5120, 1440);
        var upper = new PixelRect(856, -1440, 3440, 1440);

        Assert.Equal(new PixelRect(0, -1440, 5120, 2880), PixelRect.Union([primary, upper]));
    }

    [Fact]
    public void Union_IgnoresEmptyRectsAndIsEmptyForNone()
    {
        Assert.Equal(new PixelRect(1, 2, 3, 4), PixelRect.Union([new PixelRect(1, 2, 3, 4), default]));
        Assert.True(PixelRect.Union([]).IsEmpty);
    }
}
