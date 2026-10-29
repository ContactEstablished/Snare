using SkiaSharp;
using Snare.Core.Imaging;

namespace Snare.Rendering.Tests;

public class PixelBufferImagesTests
{
    /// <summary>Four distinct pixels, so a row-order, stride or channel mix-up shows.</summary>
    private static PixelBuffer TwoByTwo()
    {
        var buffer = PixelBuffer.Filled(2, 2, 0xFF112233);
        buffer.Pixels[4] = 0xFF;                      // (1, 0) blue  -> 0xFF1122FF
        buffer.Pixels[buffer.Stride + 2] = 0xFF;      // (0, 1) red   -> 0xFFFF2233
        buffer.Pixels[buffer.Stride + 4 + 1] = 0xFF;  // (1, 1) green -> 0xFF11FF33
        return buffer;
    }

    [Fact]
    public void ToBitmap_KeepsColorsAndLayout() => AssertPixels(PixelBufferImages.ToBitmap(TwoByTwo()));

    [Fact]
    public void ToImage_KeepsColorsAndLayout()
    {
        using var image = PixelBufferImages.ToImage(TwoByTwo());
        AssertPixels(SKBitmap.FromImage(image));
    }

    private static void AssertPixels(SKBitmap bitmap)
    {
        using (bitmap)
        {
            Assert.Equal(2, bitmap.Width);
            Assert.Equal(2, bitmap.Height);
            Assert.Equal(new SKColor(0x11, 0x22, 0x33), bitmap.GetPixel(0, 0));
            Assert.Equal(new SKColor(0x11, 0x22, 0xFF), bitmap.GetPixel(1, 0));
            Assert.Equal(new SKColor(0xFF, 0x22, 0x33), bitmap.GetPixel(0, 1));
            Assert.Equal(new SKColor(0x11, 0xFF, 0x33), bitmap.GetPixel(1, 1));
        }
    }
}
