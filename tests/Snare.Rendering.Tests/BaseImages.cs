using SkiaSharp;

namespace Snare.Rendering.Tests;

/// <summary>Deterministic test images, drawn without fonts so they're identical on every machine.</summary>
internal static class BaseImages
{
    /// <summary>320x200: a blue-to-white gradient with dark "text" bars, so blur and pixelate show.</summary>
    public static SKBitmap Sample()
    {
        var bitmap = new SKBitmap(new SKImageInfo(320, 200, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        using (var gradient = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(320, 200),
                [new SKColor(0x1F, 0x6F, 0xEB), new SKColor(0xF5, 0xF5, 0xF5)],
                SKShaderTileMode.Clamp),
        })
        {
            canvas.DrawRect(0, 0, 320, 200, gradient);
        }

        using var bar = new SKPaint { Color = new SKColor(0x20, 0x20, 0x20) };
        for (var row = 0; row < 8; row++)
        {
            for (var word = 0; word < 6; word++)
            {
                var width = 18 + (((row * 7) + (word * 13)) % 23);
                canvas.DrawRect(12 + (word * 50), 14 + (row * 22), width, 8, bar);
            }
        }

        return bitmap;
    }

    /// <summary>Pseudo-random noise with no pure-black pixels, for the redaction proof.</summary>
    public static SKBitmap Noise(int width, int height)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        var random = new Random(1234);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, new SKColor((byte)random.Next(16, 256), (byte)random.Next(16, 256), (byte)random.Next(16, 256)));
            }
        }

        return bitmap;
    }
}
