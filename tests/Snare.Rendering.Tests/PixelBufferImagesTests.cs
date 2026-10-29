using SkiaSharp;
using Snare.Core.Imaging;

namespace Snare.Rendering.Tests;

public class PixelBufferImagesTests
{
    private static PixelBuffer TwoByOne()
    {
        var buffer = PixelBuffer.Filled(2, 1, 0xFF112233);
        buffer.Pixels[4] = 0xFF; // (1, 0) blue -> 0xFF1122FF
        return buffer;
    }

    [Fact]
    public void ToBitmap_KeepsColorsAndLayout()
    {
        using var bitmap = PixelBufferImages.ToBitmap(TwoByOne());

        Assert.Equal(new SKColor(0x11, 0x22, 0x33), bitmap.GetPixel(0, 0));
        Assert.Equal(new SKColor(0x11, 0x22, 0xFF), bitmap.GetPixel(1, 0));
    }

    [Fact]
    public void ToImage_KeepsColorsAndLayout()
    {
        using var image = PixelBufferImages.ToImage(TwoByOne());
        using var bitmap = SKBitmap.FromImage(image);

        Assert.Equal(2, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal(new SKColor(0x11, 0x22, 0xFF), bitmap.GetPixel(1, 0));
    }
}
