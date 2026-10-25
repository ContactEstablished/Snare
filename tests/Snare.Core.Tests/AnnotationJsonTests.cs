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
    public void Serialize_WritesTypeDiscriminatorsAndCamelCaseEnums()
    {
        var json = AnnotationJson.Serialize(SampleDocument());

        Assert.Contains("\"type\":\"arrow\"", json, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"redact\"", json, StringComparison.Ordinal);
        Assert.Contains("\"mode\":\"solid\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_RejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => AnnotationJson.Deserialize(" "));
    }
}
