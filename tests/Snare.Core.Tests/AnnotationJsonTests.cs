using Snare.Core.Annotations;
using Snare.Core.Geometry;

namespace Snare.Core.Tests;

public class AnnotationJsonTests
{
    private static AnnotationDocument SampleDocument() => new()
    {
        Canvas = new CanvasInfo(1920, 1080, new PixelRect(10, 20, 800, 600)),
        Objects =
        [
            new ArrowAnnotation("a1", new PixelPoint(100, 200), new PixelPoint(400, 260), "#E53935", 6),
            new RectangleAnnotation("b1", new PixelRect(50, 50, 200, 100), "#1E88E5", 3, Fill: null),
            new RedactAnnotation("r1", new PixelRect(600, 120, 280, 40), RedactMode.Solid),
        ],
    };

    /// <summary>One of every tool, with non-default options, so nothing is lost on a round trip.</summary>
    private static AnnotationDocument EveryTool() => new()
    {
        Canvas = new CanvasInfo(1920, 1080, new PixelRect(0, 0, 1000, 500), new PixelSize(500, 250)),
        Objects =
        [
            new ArrowAnnotation("a", new PixelPoint(1, 2), new PixelPoint(30, 40), "#E53935", 6, ArrowHeadStyle.Open, 5),
            new LineAnnotation("l", new PixelPoint(5, 5), new PixelPoint(50, 5), "#000000", 2, Dashed: true),
            new RectangleAnnotation("r", new PixelRect(1, 1, 10, 10), "#1E88E5", 3, "#FFFF00", CornerRadius: 6),
            new EllipseAnnotation("e", new PixelRect(20, 20, 30, 15), "#43A047", 4, Fill: "#80FFFFFF"),
            new PenAnnotation("p", [new PixelPoint(0, 0), new PixelPoint(3, 4), new PixelPoint(9, 1)], "#8E24AA", 3),
            new HighlighterAnnotation("h", [new PixelPoint(10, 10), new PixelPoint(90, 10)], "#FFEB3B", 18),
            new TextAnnotation("t", new PixelPoint(40, 60), "Line one\nLine two", "#FFFFFF", 24, "Consolas", "#CC000000", Bold: true),
            new RedactAnnotation("x", new PixelRect(5, 5, 20, 20), RedactMode.Pixelate, Strength: 8),
            new StepAnnotation("s", new PixelPoint(70, 70), "#E53935", 22),
        ],
    };

    [Fact]
    public void RoundTrip_PreservesEveryObject()
    {
        var original = SampleDocument();

        var restored = AnnotationJson.Deserialize(AnnotationJson.Serialize(original));

        Assert.Equal(original.SchemaVersion, restored.SchemaVersion);
        Assert.Equal(original.Canvas, restored.Canvas);
        Assert.Equal(original.Objects, restored.Objects);
    }

    [Fact]
    public void RoundTrip_PreservesEveryToolAndOption()
    {
        var original = EveryTool();

        var restored = AnnotationJson.Deserialize(AnnotationJson.Serialize(original));

        Assert.Equal(original.Canvas, restored.Canvas);
        Assert.Equal(original.Objects, restored.Objects);
    }

    [Fact]
    public void Serialize_WritesTypeDiscriminatorsAndCamelCaseEnums()
    {
        var json = AnnotationJson.Serialize(EveryTool());

        foreach (var type in new[] { "arrow", "line", "rectangle", "ellipse", "pen", "highlighter", "text", "redact", "step" })
        {
            Assert.Contains($"\"type\":\"{type}\"", json, StringComparison.Ordinal);
        }

        Assert.Contains("\"mode\":\"pixelate\"", json, StringComparison.Ordinal);
        Assert.Contains("\"headStyle\":\"open\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_LeavesOutComputedProperties()
    {
        var json = AnnotationJson.Serialize(EveryTool());

        Assert.DoesNotContain("effectiveStrength", json, StringComparison.Ordinal);
        Assert.DoesNotContain("exportArea", json, StringComparison.Ordinal);
        Assert.DoesNotContain("outputSize", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_ReadsPhaseOneJsonWithoutTheNewOptions()
    {
        const string json = """
            {"schemaVersion":1,"canvas":{"width":1920,"height":1080,"crop":null},
             "objects":[{"type":"arrow","id":"a1","from":{"x":100,"y":200},"to":{"x":400,"y":260},"stroke":"#E53935","strokeWidth":6}]}
            """;

        var document = AnnotationJson.Deserialize(json);

        var arrow = Assert.IsType<ArrowAnnotation>(Assert.Single(document.Objects));
        Assert.Equal(ArrowHeadStyle.Filled, arrow.HeadStyle);
        Assert.Equal(4, arrow.HeadSize);
        Assert.Null(document.Canvas.Resize);
    }

    [Fact]
    public void Deserialize_RejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => AnnotationJson.Deserialize(" "));
    }

    [Fact]
    public void PenEquality_ComparesPointsByValue()
    {
        var a = new PenAnnotation("p", [new PixelPoint(1, 1)], "#000000", 2);
        var b = new PenAnnotation("p", new List<PixelPoint> { new(1, 1) }, "#000000", 2);
        var c = new PenAnnotation("p", [new PixelPoint(1, 2)], "#000000", 2);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Theory]
    [InlineData(null, null, 0, 0, 1920, 1080, 1920, 1080)]
    [InlineData(true, null, 10, 20, 800, 600, 800, 600)]
    [InlineData(true, true, 10, 20, 800, 600, 400, 300)]
    public void Canvas_ExportAreaAndOutputSize(bool? crop, bool? resize, int x, int y, int w, int h, int outW, int outH)
    {
        var canvas = new CanvasInfo(
            1920, 1080,
            crop == true ? new PixelRect(10, 20, 800, 600) : null,
            resize == true ? new PixelSize(400, 300) : null);

        Assert.Equal(new PixelRect(x, y, w, h), canvas.ExportArea);
        Assert.Equal(new PixelSize(outW, outH), canvas.OutputSize);
    }

    [Fact]
    public void Canvas_CropIsClippedToTheImage()
    {
        var canvas = new CanvasInfo(100, 100, new PixelRect(80, 80, 50, 50));

        Assert.Equal(new PixelRect(80, 80, 20, 20), canvas.ExportArea);
    }
}
