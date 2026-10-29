using SkiaSharp;
using Snare.Core.Annotations;
using Snare.Core.Geometry;
using Snare.Rendering.Editing;

namespace Snare.Rendering.Tests;

public class AnnotationGeometryTests
{
    [Fact]
    public void Line_IsHitNearTheSegmentOnly()
    {
        var line = new LineAnnotation("l", new PixelPoint(0, 0), new PixelPoint(100, 0), "#000000", 4);

        Assert.True(AnnotationGeometry.HitTest(line, new SKPoint(50, 3), 2));
        Assert.False(AnnotationGeometry.HitTest(line, new SKPoint(50, 10), 2));
        Assert.False(AnnotationGeometry.HitTest(line, new SKPoint(110, 0), 2));
    }

    [Fact]
    public void FilledRectangle_IsHitInside()
    {
        var rect = new RectangleAnnotation("r", new PixelRect(0, 0, 100, 100), "#000000", 2, "#FFFFFF");

        Assert.True(AnnotationGeometry.HitTest(rect, new SKPoint(50, 50), 2));
    }

    [Fact]
    public void Ellipse_OutlineIsHitOnTheCurve()
    {
        var ellipse = new EllipseAnnotation("e", new PixelRect(0, 0, 200, 100), "#000000", 4);

        Assert.True(AnnotationGeometry.HitTest(ellipse, new SKPoint(100, 1), 2));
        Assert.False(AnnotationGeometry.HitTest(ellipse, new SKPoint(100, 50), 2));
        Assert.False(AnnotationGeometry.HitTest(ellipse, new SKPoint(5, 5), 2)); // corner of the box, off the curve
    }

    [Fact]
    public void Pen_IsHitAlongItsPoints()
    {
        var pen = new PenAnnotation("p", [new PixelPoint(0, 0), new PixelPoint(50, 50), new PixelPoint(100, 0)], "#000000", 4);

        Assert.True(AnnotationGeometry.HitTest(pen, new SKPoint(75, 25), 2));
        Assert.False(AnnotationGeometry.HitTest(pen, new SKPoint(50, 0), 2));
    }

    [Fact]
    public void Step_IsHitWithinItsRadius()
    {
        var step = new StepAnnotation("s", new PixelPoint(50, 50), "#E53935", 18);

        Assert.True(AnnotationGeometry.HitTest(step, new SKPoint(60, 60), 0));
        Assert.False(AnnotationGeometry.HitTest(step, new SKPoint(70, 70), 0));
    }

    [Fact]
    public void Text_BoundsGrowWithLines()
    {
        var one = new TextAnnotation("t", new PixelPoint(0, 0), "Hello", "#000000", 20);
        var two = one with { Text = "Hello\nWorld" };

        var oneBounds = AnnotationGeometry.Bounds(one);
        var twoBounds = AnnotationGeometry.Bounds(two);

        Assert.True(oneBounds.Width > 30);
        Assert.True(twoBounds.Height > oneBounds.Height * 1.5f);
    }

    [Fact]
    public void Translate_MovesEveryKindOfObject()
    {
        AnnotationObject[] items =
        [
            new ArrowAnnotation("a", new PixelPoint(0, 0), new PixelPoint(10, 10), "#000000", 2),
            new PenAnnotation("p", [new PixelPoint(0, 0)], "#000000", 2),
            new TextAnnotation("t", new PixelPoint(0, 0), "x", "#000000", 10),
            new StepAnnotation("s", new PixelPoint(0, 0), "#000000"),
            new RedactAnnotation("r", new PixelRect(0, 0, 5, 5), RedactMode.Solid),
        ];

        foreach (var item in items)
        {
            var before = AnnotationGeometry.Bounds(item);
            var after = AnnotationGeometry.Bounds(AnnotationGeometry.Translate(item, 7, -3));
            Assert.Equal(before.Left + 7, after.Left, 2);
            Assert.Equal(before.Top - 3, after.Top, 2);
        }
    }

    [Fact]
    public void ResizeBox_FlipsPastTheOppositeEdge()
    {
        var box = new PixelRect(10, 10, 20, 20);

        Assert.Equal(new PixelRect(10, 10, 40, 30), AnnotationGeometry.ResizeBox(box, EditorHandle.BottomRight, new PixelPoint(50, 40)));
        Assert.Equal(new PixelRect(30, 30, 20, 20), AnnotationGeometry.ResizeBox(box, EditorHandle.TopLeft, new PixelPoint(50, 50)));
        Assert.Equal(new PixelRect(10, 5, 20, 25), AnnotationGeometry.ResizeBox(box, EditorHandle.Top, new PixelPoint(99, 5)));
    }

    [Fact]
    public void Handles_EightForBoxes_TwoForLines_NoneForStrokes()
    {
        Assert.Equal(8, AnnotationGeometry.HandlesOf(new EllipseAnnotation("e", new PixelRect(0, 0, 10, 10), "#000000", 1)).Count);
        Assert.Equal(2, AnnotationGeometry.HandlesOf(new LineAnnotation("l", new PixelPoint(0, 0), new PixelPoint(5, 5), "#000000", 1)).Count);
        Assert.Empty(AnnotationGeometry.HandlesOf(new PenAnnotation("p", [new PixelPoint(0, 0)], "#000000", 1)));
    }
}
