using SkiaSharp;
using Snare.Core.Geometry;

namespace Snare.Rendering.Tests;

public class ImageCodecTests
{
    private static SKBitmap Checker()
    {
        var bitmap = new SKBitmap(new SKImageInfo(64, 32, SKColorType.Bgra8888, SKAlphaType.Opaque));
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                bitmap.SetPixel(x, y, x < 32 ? new SKColor(0xE5, 0x39, 0x35) : new SKColor(0x1F, 0x6F, 0xEB));
            }
        }

        return bitmap;
    }

    [Fact]
    public void Png_RoundTripsExactly()
    {
        using var original = Checker();

        using var decoded = ImageCodec.Decode(ImageCodec.EncodePng(original));

        Assert.Equal(original.Width, decoded.Width);
        Assert.Equal(original.Height, decoded.Height);
        Assert.Equal(original.GetPixel(5, 5), decoded.GetPixel(5, 5));
        Assert.Equal(original.GetPixel(60, 30), decoded.GetPixel(60, 30));
    }

    [Fact]
    public void Png_HasNoTextOrExifChunks()
    {
        using var original = Checker();

        var png = System.Text.Encoding.Latin1.GetString(ImageCodec.EncodePng(original));

        Assert.DoesNotContain("tEXt", png, StringComparison.Ordinal);
        Assert.DoesNotContain("iTXt", png, StringComparison.Ordinal);
        Assert.DoesNotContain("eXIf", png, StringComparison.Ordinal);
    }

    [Fact]
    public void Jpeg_DecodesCloseToTheOriginal()
    {
        using var original = Checker();

        using var decoded = ImageCodec.Decode(ImageCodec.EncodeJpeg(original, 90));
        var pixel = decoded.GetPixel(10, 10);

        Assert.InRange(pixel.Red, 0xE5 - 12, 0xE5 + 12);
        Assert.InRange(pixel.Blue, 0x35 - 12, 0x35 + 12);
    }

    [Fact]
    public void Thumbnail_IsWebpThatFitsTheBox()
    {
        using var original = Checker();

        var webp = ImageCodec.EncodeThumbnail(original);
        using var decoded = ImageCodec.Decode(webp);

        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(webp, 0, 4));
        Assert.Equal("WEBP", System.Text.Encoding.ASCII.GetString(webp, 8, 4));
        Assert.Equal(64, decoded.Width); // never upscaled
    }

    [Fact]
    public void Thumbnail_OfAVeryTallImage_ShowsItsTop()
    {
        // 400x4000: red top 300 rows (the 4:3 crop), blue below.
        using var tall = new SKBitmap(new SKImageInfo(400, 4000, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(tall))
        {
            canvas.Clear(SKColors.Blue);
            using var red = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0, 0, 400, 300, red);
        }

        using var decoded = ImageCodec.Decode(ImageCodec.EncodeThumbnail(tall));

        Assert.Equal((320, 240), (decoded.Width, decoded.Height));
        Assert.InRange(decoded.GetPixel(160, 230).Red, 200, 255); // still the top's red at the bottom edge
    }

    [Theory]
    [InlineData(1920, 30000, 1920, 1440)]
    [InlineData(400, 801, 400, 300)]
    [InlineData(400, 800, 400, 800)]
    [InlineData(5120, 1440, 5120, 1440)]
    public void ThumbnailSource_CropsOnlyVeryTallImages(int width, int height, int expectedWidth, int expectedHeight)
    {
        Assert.Equal(new PixelSize(expectedWidth, expectedHeight), ImageCodec.ThumbnailSource(new PixelSize(width, height)));
    }

    [Fact]
    public void Decode_RejectsBytesThatArentAnImage()
    {
        Assert.Throws<InvalidDataException>(() => ImageCodec.Decode("not really a png"u8.ToArray()));
    }

    [Theory]
    [InlineData(5120, 1440, 320, 90)]
    [InlineData(5120, 2880, 320, 180)]
    [InlineData(1000, 30000, 8, 240)]
    [InlineData(100, 50, 100, 50)]
    [InlineData(10000, 1, 320, 1)]
    public void ThumbnailSize_KeepsAspectRatioInside320x240(int width, int height, int expectedWidth, int expectedHeight)
    {
        Assert.Equal(new PixelSize(expectedWidth, expectedHeight), ImageCodec.ThumbnailSize(new PixelSize(width, height)));
    }
}
