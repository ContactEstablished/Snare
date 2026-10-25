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
}
