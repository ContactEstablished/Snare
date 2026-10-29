using Microsoft.Extensions.Logging;
using SkiaSharp;
using Snare.Capture;
using Snare.Core.Captures;
using Snare.Rendering;

namespace Snare.App.Spikes;

/// <summary>Phase 0 spike: opens the Skia canvas on a test pattern or a frozen desktop.</summary>
public sealed class SkiaCanvasSpike(ILogger<SkiaCanvasSpike> logger)
{
    /// <summary>Both of Matt's monitors stacked: 5120 wide, 1440 + 1440 tall.</summary>
    private const int PatternWidth = 5120;
    private const int PatternHeight = 2880;

    public void RunWithTestPattern()
    {
        logger.LogInformation("Opening Skia canvas spike on a {Width}x{Height} test pattern", PatternWidth, PatternHeight);
        new SkiaCanvasWindow(CreateTestPattern(PatternWidth, PatternHeight), "test pattern").Show();
    }

    public void RunWithDesktop(FrozenDesktop desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        var composed = VirtualDesktopComposer.Compose(desktop);
        logger.LogInformation("Opening Skia canvas spike on the frozen desktop, {Width}x{Height}", composed.Width, composed.Height);
        new SkiaCanvasWindow(PixelBufferImages.ToImage(composed), "frozen desktop").Show();
    }

    /// <summary>Fine detail everywhere (grid, text, gradients) so scaling cost is realistic.</summary>
    private static SKImage CreateTestPattern(int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        var canvas = surface.Canvas;

        using var gradient = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(width, height),
                [new SKColor(0x1F, 0x6F, 0xEB), new SKColor(0xF5, 0xF5, 0xF5), new SKColor(0x2E, 0x7D, 0x32)],
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(0, 0, width, height, gradient);

        using var grid = new SKPaint { Color = new SKColor(0, 0, 0, 0x40), StrokeWidth = 1 };
        for (var x = 0; x < width; x += 20)
        {
            canvas.DrawLine(x, 0, x, height, grid);
        }

        for (var y = 0; y < height; y += 20)
        {
            canvas.DrawLine(0, y, width, y, grid);
        }

        using var font = new SKFont(SKTypeface.Default, 14);
        using var text = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        for (var y = 40; y < height; y += 200)
        {
            for (var x = 20; x < width; x += 400)
            {
                canvas.DrawText($"({x}, {y}) the quick brown fox", x, y, SKTextAlign.Left, font, text);
            }
        }

        return surface.Snapshot();
    }
}
