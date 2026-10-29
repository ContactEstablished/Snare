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

    [Theory]
    [InlineData(RedactMode.Pixelate, 2)]
    [InlineData(RedactMode.Blur, 3)]
    public void BlurOrPixelate_OverASolidRedaction_CannotRevealIt(RedactMode mode, float strength)
    {
        using var original = BaseImages.Noise(200, 100);
        var document = new AnnotationDocument
        {
            Canvas = new CanvasInfo(200, 100),
            Objects =
            [
                new RedactAnnotation("r", Secret, RedactMode.Solid, "#000000"),
                // Covers the secret and spills past it, and comes later in z-order.
                new RedactAnnotation("o", new PixelRect(Secret.X - 10, Secret.Y - 5, Secret.Width + 20, Secret.Height + 10), mode, Strength: strength),
            ],
        };

        using var export = new AnnotationRenderer().Render(original, document);

        // Pixelate and blur of an all-black area stay black, away from the edges the blur may soften (3 sigma).
        var margin = (int)Math.Max(8, (strength * 3) + 1);
        for (var y = Secret.Y + margin; y < Secret.Bottom - margin; y++)
        {
            for (var x = Secret.X + margin; x < Secret.Right - margin; x++)
            {
                var c = export.GetPixel(x, y);
                Assert.True(c.Red < 8 && c.Green < 8 && c.Blue < 8, $"({x}, {y}) is {c}");
            }
        }
    }

    [Fact]
    public void SolidRedaction_WithResize_KeepsNoOriginalPixelsInside()
    {
        using var original = BaseImages.Noise(200, 100);
        var document = new AnnotationDocument
        {
            Canvas = new CanvasInfo(200, 100, Resize: new PixelSize(100, 50)),
            Objects = [new RedactAnnotation("r", Secret, RedactMode.Solid)],
        };

        using var export = new AnnotationRenderer().Render(original, document);

        // Every output pixel whose source footprint is fully inside the box is pure black.
        for (var y = (Secret.Y / 2) + 2; y < (Secret.Bottom / 2) - 2; y++)
        {
            for (var x = (Secret.X / 2) + 2; x < (Secret.Right / 2) - 2; x++)
            {
                Assert.Equal(SKColors.Black, export.GetPixel(x, y));
            }
        }
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
