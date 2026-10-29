using SkiaSharp;
using Snare.Core.Annotations;
using Snare.Core.Geometry;
using Snare.Rendering.Editing;

namespace Snare.Rendering.Tests;

public sealed class CalloutTests : IDisposable
{
    private readonly SKBitmap _original = BaseImages.Sample();

    public void Dispose() => _original.Dispose();

    private static CalloutAnnotation Callout(PixelPoint tail, string text = "Hi") =>
        new("c", new PixelRect(100, 50, 100, 40), tail, text, "#E53935", 18);

    private EditorSession Session(params AnnotationObject[] objects) =>
        new(_original, new AnnotationDocument { Canvas = new CanvasInfo(320, 200), Objects = [.. objects] });

    [Theory]
    [InlineData(150, 160, 'B')] // below: leaves from the bottom
    [InlineData(150, 5, 'T')]
    [InlineData(300, 70, 'R')]
    [InlineData(10, 75, 'L')]
    public void Tail_LeavesFromTheSideNearestThePoint(int x, int y, char side)
    {
        var tail = CalloutShape.Tail(Callout(new PixelPoint(x, y)))!.Value;

        var (edgeA, edgeB) = side switch
        {
            'B' => (tail.BaseA.Y, 90f),
            'T' => (tail.BaseA.Y, 50f),
            'R' => (tail.BaseA.X, 200f),
            _ => (tail.BaseA.X, 100f),
        };
        Assert.Equal(edgeB, edgeA);
        Assert.Equal(new SKPoint(x, y), tail.Tip);
    }

    [Fact]
    public void Tail_InsideTheBubble_IsNone()
    {
        Assert.Null(CalloutShape.Tail(Callout(new PixelPoint(150, 70))));
    }

    [Fact]
    public void Geometry_CoversTheTail_HitsIt_AndMovesItWithTheBubble()
    {
        var callout = Callout(new PixelPoint(120, 160));

        var bounds = AnnotationGeometry.Bounds(callout);
        Assert.True(bounds.Bottom >= 160);
        Assert.True(AnnotationGeometry.HitTest(callout, new SKPoint(150, 70), 2));    // the bubble
        Assert.True(AnnotationGeometry.HitTest(callout, new SKPoint(121, 155), 2));   // near the tip
        Assert.False(AnnotationGeometry.HitTest(callout, new SKPoint(20, 180), 2));

        var moved = Assert.IsType<CalloutAnnotation>(AnnotationGeometry.Translate(callout, 10, -5));
        Assert.Equal((new PixelRect(110, 45, 100, 40), new PixelPoint(130, 155)), (moved.Bounds, moved.Tail));
        Assert.Contains(AnnotationGeometry.HandlesOf(callout), h => h.Handle == EditorHandle.Tail && h.Position == new SKPoint(120, 160));
    }

    [Fact]
    public void Draw_ThenType_AddsTheCallout()
    {
        var s = Session();
        s.Tool = EditorTool.Callout;
        TextAnnotation? requested = null;
        s.TextEditRequested += (_, t) => requested = t;

        s.PointerDown(new SKPoint(40, 30));
        s.PointerMove(new SKPoint(160, 80));
        s.PointerUp(new SKPoint(160, 80));

        Assert.Empty(s.Objects); // not yet: it waits for its text
        Assert.IsType<CalloutAnnotation>(s.Preview);
        Assert.NotNull(requested);
        Assert.True(requested.Position.X > 40 && requested.Position.Y > 30); // inside the bubble's padding

        s.CommitText(requested, "Look here");

        var callout = Assert.IsType<CalloutAnnotation>(Assert.Single(s.Objects));
        Assert.Equal(new PixelRect(40, 30, 120, 50), callout.Bounds);
        Assert.Equal("Look here", callout.Text);
        Assert.True(callout.Tail.Y > callout.Bounds.Bottom); // the tail starts below
        Assert.Null(s.Preview);
        Assert.Contains(callout.Id, s.Selection);
    }

    [Fact]
    public void Draw_ThenLeaveItEmpty_AddsNothing()
    {
        var s = Session();
        s.Tool = EditorTool.Callout;
        TextAnnotation? requested = null;
        s.TextEditRequested += (_, t) => requested = t;
        s.PointerDown(new SKPoint(40, 30));
        s.PointerUp(new SKPoint(160, 80));

        s.CommitText(requested!, "  ");

        Assert.Empty(s.Objects);
        Assert.Null(s.Preview);
    }

    [Fact]
    public void DoubleClick_EditsTheText_AndUndoRestoresIt()
    {
        var s = Session(Callout(new PixelPoint(120, 160), "Old"));
        TextAnnotation? requested = null;
        s.TextEditRequested += (_, t) => requested = t;

        s.DoubleClick(new SKPoint(150, 70));
        Assert.Equal("Old", requested!.Text);
        s.CommitText(requested, "New");

        Assert.Equal("New", Assert.IsType<CalloutAnnotation>(Assert.Single(s.Objects)).Text);
        s.Undo();
        Assert.Equal("Old", Assert.IsType<CalloutAnnotation>(Assert.Single(s.Objects)).Text);
    }

    [Fact]
    public void DraggingTheTailHandle_MovesOnlyTheTail()
    {
        var s = Session(Callout(new PixelPoint(120, 160)));
        s.Tool = EditorTool.Select;
        s.PointerDown(new SKPoint(150, 70)); // select it
        s.PointerUp(new SKPoint(150, 70));

        s.PointerDown(new SKPoint(120, 160));
        s.PointerMove(new SKPoint(250, 20));
        s.PointerUp(new SKPoint(250, 20));

        var callout = Assert.IsType<CalloutAnnotation>(Assert.Single(s.Objects));
        Assert.Equal(new PixelPoint(250, 20), callout.Tail);
        Assert.Equal(new PixelRect(100, 50, 100, 40), callout.Bounds);
    }

    [Fact]
    public void UndoWhileEditingItsText_DoesntTurnItIntoPlainText()
    {
        var s = Session();
        s.Tool = EditorTool.Callout;
        TextAnnotation? requested = null;
        s.TextEditRequested += (_, t) => requested = t;
        s.PointerDown(new SKPoint(40, 30));
        s.PointerUp(new SKPoint(160, 80));
        s.CommitText(requested!, "First");

        s.DoubleClick(new SKPoint(100, 50)); // edit it again...
        s.Undo();                             // ...and undo adding it while the box is open
        s.CommitText(requested!, "Second");

        Assert.Empty(s.Objects);
    }

    [Fact]
    public void ANewCallout_NearTheBottom_PointsUpInsideTheImage()
    {
        var s = Session();
        s.Tool = EditorTool.Callout;
        s.PointerDown(new SKPoint(40, 150));
        s.PointerUp(new SKPoint(160, 190));

        var pending = Assert.IsType<CalloutAnnotation>(s.Preview);
        Assert.InRange(pending.Tail.Y, 0, 149);
    }

    [Fact]
    public void NoFill_IsSeeThrough_AndShowsAsNoFill()
    {
        var s = Session(Callout(new PixelPoint(120, 160)));
        s.Tool = EditorTool.Select;
        s.PointerDown(new SKPoint(150, 70));
        s.PointerUp(new SKPoint(150, 70));

        s.ApplyStyleToSelection(style => style with { FillColor = null });

        var callout = Assert.IsType<CalloutAnnotation>(Assert.Single(s.Objects));
        Assert.Equal("#00000000", callout.Fill);
        Assert.Null(s.StyleOf(callout).FillColor);
    }

    [Fact]
    public void Json_LeavesOutComputedPadding()
    {
        var json = AnnotationJson.Serialize(new AnnotationDocument { Canvas = new CanvasInfo(10, 10), Objects = [Callout(new PixelPoint(1, 2))] });

        Assert.DoesNotContain("padding", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var document = new AnnotationDocument { Canvas = new CanvasInfo(320, 200), Objects = [Callout(new PixelPoint(1, 2), "a\nb")] };

        var back = AnnotationJson.Deserialize(AnnotationJson.Serialize(document));

        Assert.Equal(document.Objects[0], back.Objects[0]);
        Assert.Contains("\"type\":\"callout\"", AnnotationJson.Serialize(document), StringComparison.Ordinal);
    }
}
