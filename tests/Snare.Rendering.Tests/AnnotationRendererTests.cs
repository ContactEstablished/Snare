using SkiaSharp;
using Snare.Core.Annotations;

namespace Snare.Rendering.Tests;

public class AnnotationRendererTests
{
    [Fact]
    public void Render_WithNoObjects_ReturnsAnIdenticalCopy()
    {
        using var original = new SKBitmap(4, 3);
        original.Erase(SKColors.White);
        original.SetPixel(2, 1, SKColors.Red);
        var document = new AnnotationDocument { Canvas = new CanvasInfo(4, 3) };

        using var result = new AnnotationRenderer().Render(original, document);

        Assert.NotSame(original, result);
        Assert.Equal(original.Width, result.Width);
        Assert.Equal(original.Height, result.Height);
        Assert.Equal(SKColors.Red, result.GetPixel(2, 1));
        Assert.Equal(SKColors.White, result.GetPixel(0, 0));
    }
}
