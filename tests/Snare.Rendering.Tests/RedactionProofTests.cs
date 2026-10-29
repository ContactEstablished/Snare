using SkiaSharp;
using Snare.Core.Annotations;
using Snare.Core.Geometry;

namespace Snare.Rendering.Tests;

/// <summary>Solid redaction is the only tool described as secure; these prove the export keeps no original pixels.</summary>
public class RedactionProofTests
{
    private static readonly PixelRect Secret = new(37, 21, 101, 43);

    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#80000000")] // a translucent fill is forced opaque
    public void SolidRedaction_LeavesOnlyTheFillColor(string fill)
    {
        using var original = BaseImages.Noise(200, 100);
        var document = new AnnotationDocument
        {
            Canvas = new CanvasInfo(200, 100),
            Objects = [new RedactAnnotation("r", Secret, RedactMode.Solid, fill)],
        };

        using var export = new AnnotationRenderer().Render(original, document);

        var expected = SkiaColors.Parse(fill, SKColors.Black).WithAlpha(0xFF);
        for (var y = Secret.Y; y < Secret.Bottom; y++)
        {
            for (var x = Secret.X; x < Secret.Right; x++)
            {
                Assert.Equal(expected, export.GetPixel(x, y));
            }
        }

        // And just outside the box, the original is untouched.
        Assert.Equal(original.GetPixel(Secret.X - 1, Secret.Y), export.GetPixel(Secret.X - 1, Secret.Y));
        Assert.Equal(original.GetPixel(Secret.Right, Secret.Bottom - 1), export.GetPixel(Secret.Right, Secret.Bottom - 1));
    }

    [Fact]
    public void SolidRedaction_UnderAnotherAnnotation_StillCoversEverything()
    {
        using var original = BaseImages.Noise(200, 100);
        var document = new AnnotationDocument
        {
            Canvas = new CanvasInfo(200, 100, Crop: new PixelRect(20, 10, 160, 80)),
            Objects =
            [
                new RedactAnnotation("r", Secret, RedactMode.Solid),
                new HighlighterAnnotation("h", [new PixelPoint(30, 40), new PixelPoint(150, 40)], "#FFEB3B", 20),
            ],
        };

        using var export = new AnnotationRenderer().Render(original, document);

        // With the crop, original (x, y) is export (x - 20, y - 10). No secret pixel survives anywhere in the box.
        for (var y = Secret.Y; y < Secret.Bottom; y++)
        {
            for (var x = Secret.X; x < Secret.Right; x++)
            {
                Assert.NotEqual(original.GetPixel(x, y), export.GetPixel(x - 20, y - 10));
            }
        }
    }
}
