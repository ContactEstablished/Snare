using SkiaSharp;
using Snare.Core.Annotations;
using Snare.Core.Geometry;
using Snare.Rendering.Editing;

namespace Snare.Rendering.Tests;

public sealed class EditorSessionTests : IDisposable
{
    private readonly SKBitmap _original = BaseImages.Sample();

    public void Dispose() => _original.Dispose();

    private EditorSession Session(params AnnotationObject[] objects) =>
        new(_original, new AnnotationDocument { Canvas = new CanvasInfo(320, 200), Objects = [.. objects] });

    private static void Drag(EditorSession s, float x1, float y1, float x2, float y2, bool shift = false)
    {
        s.PointerDown(new SKPoint(x1, y1), shift);
        s.PointerMove(new SKPoint((x1 + x2) / 2, (y1 + y2) / 2), shift);
        s.PointerMove(new SKPoint(x2, y2), shift);
        s.PointerUp(new SKPoint(x2, y2), shift);
    }

    private static void Click(EditorSession s, float x, float y, bool shift = false)
    {
        s.PointerDown(new SKPoint(x, y), shift);
        s.PointerUp(new SKPoint(x, y), shift);
    }

    private static RectangleAnnotation Box(string id, int x, int y, int w = 40, int h = 30, string? fill = "#FFFFFF") =>
        new(id, new PixelRect(x, y, w, h), "#E53935", 4, fill);

    [Fact]
    public void Rectangle_Drag_AddsAndSelectsIt()
    {
        var s = Session();
        s.Tool = EditorTool.Rectangle;

        Drag(s, 10, 20, 110, 70);

        var rect = Assert.IsType<RectangleAnnotation>(Assert.Single(s.Objects));
        Assert.Equal(new PixelRect(10, 20, 100, 50), rect.Bounds);
        Assert.Equal("#E53935", rect.Stroke);
        Assert.Contains(rect.Id, s.Selection);
        Assert.True(s.HasUnsavedChanges);
        Assert.Null(s.Preview);
    }

    [Fact]
    public void Shape_PreviewShowsWhileDragging()
    {
        var s = Session();
        s.Tool = EditorTool.Ellipse;

        s.PointerDown(new SKPoint(10, 10));
        s.PointerMove(new SKPoint(50, 40));

        Assert.IsType<EllipseAnnotation>(s.Preview);
        Assert.Empty(s.Objects);
    }

    [Fact]
    public void TinyDrag_AddsNothing()
    {
        var s = Session();
        s.Tool = EditorTool.Rectangle;

        Drag(s, 10, 10, 11, 11);

        Assert.Empty(s.Objects);
    }

    [Fact]
    public void Shift_MakesSquaresAnd45DegreeArrows()
    {
        var s = Session();
        s.Tool = EditorTool.Rectangle;
        Drag(s, 10, 10, 60, 30, shift: true);
        Assert.Equal(new PixelRect(10, 10, 50, 50), ((RectangleAnnotation)s.Objects[0]).Bounds);

        s.Tool = EditorTool.Arrow;
        Drag(s, 100, 100, 200, 110, shift: true);
        var arrow = (ArrowAnnotation)s.Objects[1];
        Assert.Equal(arrow.From.Y, arrow.To.Y); // snapped to horizontal
    }

    [Theory]
    [InlineData(EditorTool.Redact, RedactMode.Solid)]
    [InlineData(EditorTool.Blur, RedactMode.Blur)]
    [InlineData(EditorTool.Pixelate, RedactMode.Pixelate)]
    public void RedactTools_MakeTheRightMode(EditorTool tool, RedactMode mode)
    {
        var s = Session();
        s.Tool = tool;

        Drag(s, 10, 10, 60, 40);

        Assert.Equal(mode, Assert.IsType<RedactAnnotation>(Assert.Single(s.Objects)).Mode);
    }

    [Fact]
    public void Pen_CollectsPointsAndSkipsJitter()
    {
        var s = Session();
        s.Tool = EditorTool.Pen;

        s.PointerDown(new SKPoint(10, 10));
        s.PointerMove(new SKPoint(10.4f, 10.2f)); // under 1.5 px: dropped
        s.PointerMove(new SKPoint(20, 15));
        s.PointerMove(new SKPoint(30, 25));
        s.PointerUp(new SKPoint(30, 25));

        var pen = Assert.IsType<PenAnnotation>(Assert.Single(s.Objects));
        Assert.Equal([new PixelPoint(10, 10), new PixelPoint(20, 15), new PixelPoint(30, 25)], pen.Points);
    }

    [Fact]
    public void Highlighter_WithShift_StaysHorizontal()
    {
        var s = Session();
        s.Tool = EditorTool.Highlighter;

        Drag(s, 10, 50, 200, 63, shift: true);

        var marker = Assert.IsType<HighlighterAnnotation>(Assert.Single(s.Objects));
        Assert.All(marker.Points, p => Assert.Equal(50, p.Y));
    }

    [Fact]
    public void Undo_And_Redo_EachAction()
    {
        var s = Session();
        s.Tool = EditorTool.Rectangle;
        Drag(s, 10, 10, 50, 50);
        Drag(s, 100, 100, 150, 150);

        s.Undo();
        Assert.Single(s.Objects);
        s.Undo();
        Assert.Empty(s.Objects);
        Assert.False(s.History.CanUndo);

        s.Redo();
        s.Redo();
        Assert.Equal(2, s.Objects.Count);
        Assert.False(s.History.CanRedo);
    }

    [Fact]
    public void NewAction_ClearsRedo()
    {
        var s = Session();
        s.Tool = EditorTool.Rectangle;
        Drag(s, 10, 10, 50, 50);
        s.Undo();

        Drag(s, 100, 100, 150, 150);

        Assert.False(s.History.CanRedo);
    }

    [Fact]
    public void Select_DragMovesTheObject_AsOneUndoStep()
    {
        var s = Session(Box("a", 10, 10));

        Drag(s, 30, 25, 80, 65);

        Assert.Equal(new PixelRect(60, 50, 40, 30), ((RectangleAnnotation)s.Objects[0]).Bounds);
        s.Undo();
        Assert.Equal(new PixelRect(10, 10, 40, 30), ((RectangleAnnotation)s.Objects[0]).Bounds);
        Assert.False(s.History.CanUndo);
    }

    [Fact]
    public void Select_ClickWithoutMoving_AddsNoHistory()
    {
        var s = Session(Box("a", 10, 10));

        Click(s, 30, 25);

        Assert.Equal(["a"], s.Selection);
        Assert.False(s.History.CanUndo);
    }

    [Fact]
    public void Select_CornerHandle_Resizes()
    {
        var s = Session(Box("a", 10, 10));
        Click(s, 30, 25);

        Drag(s, 50, 40, 90, 70); // bottom-right handle

        Assert.Equal(new PixelRect(10, 10, 80, 60), ((RectangleAnnotation)s.Objects[0]).Bounds);
    }

    [Fact]
    public void Select_ArrowEndpoint_Moves()
    {
        var s = Session(new ArrowAnnotation("a", new PixelPoint(10, 10), new PixelPoint(100, 10), "#E53935", 6));
        Click(s, 50, 10);

        Drag(s, 100, 10, 120, 80);

        var arrow = (ArrowAnnotation)s.Objects[0];
        Assert.Equal(new PixelPoint(10, 10), arrow.From);
        Assert.Equal(new PixelPoint(120, 80), arrow.To);
    }

    [Fact]
    public void Select_ShiftClickTogglesAndMarqueeSelects()
    {
        var s = Session(Box("a", 10, 10), Box("b", 100, 10), Box("c", 200, 150));

        Click(s, 30, 25);
        Click(s, 120, 25, shift: true);
        Assert.Equal(["a", "b"], s.Selection.Order());
        Click(s, 30, 25, shift: true);
        Assert.Equal(["b"], s.Selection);

        Drag(s, 0, 0, 160, 60); // marquee over a and b
        Assert.Equal(["a", "b"], s.Selection.Order());
    }

    [Fact]
    public void Select_ClickEmptySpace_ClearsSelection()
    {
        var s = Session(Box("a", 10, 10));
        Click(s, 30, 25);

        Click(s, 300, 190);

        Assert.Empty(s.Selection);
    }

    [Fact]
    public void OutlineRectangle_IsHitOnItsEdgeNotItsMiddle()
    {
        var s = Session(Box("a", 10, 10, 100, 80, fill: null));

        Click(s, 60, 50);
        Assert.Empty(s.Selection);

        Click(s, 11, 50);
        Assert.Equal(["a"], s.Selection);
    }

    [Fact]
    public void Delete_RemovesSelection_AndUndoRestoresZOrder()
    {
        var s = Session(Box("a", 10, 10), Box("b", 100, 10), Box("c", 200, 10));
        s.Select("b");

        s.DeleteSelection();
        Assert.Equal(["a", "c"], s.Objects.Select(o => o.Id));

        s.Undo();
        Assert.Equal(["a", "b", "c"], s.Objects.Select(o => o.Id));
    }

    [Fact]
    public void ZOrder_CommandsReorder()
    {
        var s = Session(Box("a", 0, 0), Box("b", 50, 0), Box("c", 100, 0), Box("d", 150, 0));

        s.Select("a");
        s.BringToFront();
        Assert.Equal(["b", "c", "d", "a"], s.Objects.Select(o => o.Id));

        s.SendBackward();
        Assert.Equal(["b", "c", "a", "d"], s.Objects.Select(o => o.Id));

        s.SendToBack();
        Assert.Equal(["a", "b", "c", "d"], s.Objects.Select(o => o.Id));

        s.BringForward();
        Assert.Equal(["b", "a", "c", "d"], s.Objects.Select(o => o.Id));

        s.Undo();
        Assert.Equal(["a", "b", "c", "d"], s.Objects.Select(o => o.Id));
    }

    [Fact]
    public void Nudge_MovesSelection()
    {
        var s = Session(Box("a", 10, 10));
        s.Select("a");

        s.Nudge(10, -1);

        Assert.Equal(new PixelRect(20, 9, 40, 30), ((RectangleAnnotation)s.Objects[0]).Bounds);
    }

    [Fact]
    public void Steps_AreAddedByClicking()
    {
        var s = Session();
        s.Tool = EditorTool.Step;

        Click(s, 20, 20);
        Click(s, 60, 20);

        Assert.Equal(2, s.Objects.OfType<StepAnnotation>().Count());
        Assert.Equal(new PixelPoint(60, 20), ((StepAnnotation)s.Objects[1]).Center);
    }

    [Fact]
    public void Text_ClickRequestsEditing_AndCommitAddsIt()
    {
        var s = Session();
        s.Tool = EditorTool.Text;
        TextAnnotation? requested = null;
        s.TextEditRequested += (_, t) => requested = t;

        Click(s, 40, 50);
        Assert.NotNull(requested);
        Assert.Empty(s.Objects); // nothing until text is typed

        s.CommitText(requested, "Hello");
        var text = Assert.IsType<TextAnnotation>(Assert.Single(s.Objects));
        Assert.Equal(("Hello", new PixelPoint(40, 50)), (text.Text, text.Position));
    }

    [Fact]
    public void Text_BlankCommit_AddsNothing_OrRemovesTheExisting()
    {
        var existing = new TextAnnotation("t", new PixelPoint(10, 10), "Old", "#000000", 20);
        var s = Session(existing);

        s.CommitText(existing with { Id = "new" }, "   ");
        Assert.Single(s.Objects);

        s.CommitText(existing, "New text");
        Assert.Equal("New text", ((TextAnnotation)s.Objects[0]).Text);

        s.CommitText(existing, "");
        Assert.Empty(s.Objects);

        s.Undo();
        Assert.Equal("New text", ((TextAnnotation)s.Objects[0]).Text);
    }

    [Fact]
    public void DoubleClickOnText_RequestsEditing()
    {
        var existing = new TextAnnotation("t", new PixelPoint(10, 10), "Hello", "#000000", 20);
        var s = Session(existing);
        TextAnnotation? requested = null;
        s.TextEditRequested += (_, t) => requested = t;

        s.DoubleClick(new SKPoint(20, 20));

        Assert.Equal(existing, requested);
    }

    [Fact]
    public void Eyedropper_SetsThePreviousToolsColor_AndReturnsToIt()
    {
        var s = Session();
        s.Tool = EditorTool.Arrow;
        s.Tool = EditorTool.Eyedropper;

        Click(s, 14, 16); // inside a dark bar of the sample image

        Assert.Equal(EditorTool.Arrow, s.Tool);
        Assert.Equal("#202020", s.Styles[EditorTool.Arrow].Color);
        Assert.Equal("#202020", s.RecentColors[0]);
    }

    [Fact]
    public void RecentColors_AreDistinctNewestFirstAndCapped()
    {
        var s = Session();
        for (var i = 0; i < 10; i++)
        {
            s.SetStyle(EditorTool.Pen, ToolStyle.DefaultFor(EditorTool.Pen) with { Color = $"#00000{i}" });
        }

        s.SetStyle(EditorTool.Pen, ToolStyle.DefaultFor(EditorTool.Pen) with { Color = "#000005" });

        Assert.Equal(EditorSession.RecentColorLimit, s.RecentColors.Count);
        Assert.Equal("#000005", s.RecentColors[0]);
        Assert.Single(s.RecentColors, c => c == "#000005");
    }

    [Fact]
    public void ApplyStyleToSelection_RestylesInOneUndoStep()
    {
        var s = Session(Box("a", 0, 0), Box("b", 50, 0));
        s.SelectAll();

        s.ApplyStyleToSelection(new ToolStyle("#1E88E5", 8, FillColor: null));

        Assert.All(s.Objects.Cast<RectangleAnnotation>(), r => Assert.Equal(("#1E88E5", 8f, (string?)null), (r.Stroke, r.StrokeWidth, r.Fill)));
        s.Undo();
        Assert.All(s.Objects.Cast<RectangleAnnotation>(), r => Assert.Equal("#E53935", r.Stroke));
    }

    [Fact]
    public void Crop_DrawThenApply_SetsTheCanvasAndUndoes()
    {
        var s = Session();
        s.Tool = EditorTool.Crop;

        Drag(s, 20, 30, 220, 130);
        Assert.Equal(new PixelRect(20, 30, 200, 100), s.PendingCrop);

        s.ApplyCrop();
        Assert.Equal(new PixelRect(20, 30, 200, 100), s.Document.Canvas.Crop);
        Assert.Equal(EditorTool.Select, s.Tool);

        s.Undo();
        Assert.Null(s.Document.Canvas.Crop);
    }

    [Fact]
    public void Crop_IsClippedToTheImage()
    {
        var s = Session();
        s.Tool = EditorTool.Crop;

        Drag(s, 300, 180, 500, 400);

        Assert.Equal(new PixelRect(300, 180, 20, 20), s.PendingCrop);
    }

    [Fact]
    public void Resize_SetsOutputSize_AndNaturalSizeClearsIt()
    {
        var s = Session();

        s.Resize(new PixelSize(160, 100));
        Assert.Equal(new PixelSize(160, 100), s.Document.Canvas.OutputSize);

        s.Resize(new PixelSize(320, 200));
        Assert.Null(s.Document.Canvas.Resize);
    }

    [Fact]
    public void Escape_DuringAMove_PutsObjectsBack()
    {
        var s = Session(Box("a", 10, 10));
        s.PointerDown(new SKPoint(30, 25));
        s.PointerMove(new SKPoint(130, 125));

        s.Escape();

        Assert.Equal(new PixelRect(10, 10, 40, 30), ((RectangleAnnotation)s.Objects[0]).Bounds);
        Assert.False(s.History.CanUndo);
    }

    [Fact]
    public void ToDocument_RoundTripsThroughJson()
    {
        var s = Session();
        s.Tool = EditorTool.Arrow;
        Drag(s, 10, 10, 100, 80);
        s.Tool = EditorTool.Step;
        Click(s, 150, 150);

        var json = AnnotationJson.Serialize(s.ToDocument());
        var restored = AnnotationJson.Deserialize(json);

        Assert.Equal(s.Objects, restored.Objects);
    }

    [Fact]
    public void SwitchingTool_ClearsSelection()
    {
        var s = Session(Box("a", 10, 10));
        s.Select("a");

        s.Tool = EditorTool.Pen;

        Assert.Empty(s.Selection);
    }
}
