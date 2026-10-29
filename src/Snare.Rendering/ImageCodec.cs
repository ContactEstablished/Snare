using SkiaSharp;
using Snare.Core.Geometry;

namespace Snare.Rendering;

/// <summary>
/// Encodes and decodes capture files. Skia's encoders write no EXIF or text chunks,
/// so exports carry no metadata.
/// </summary>
public static class ImageCodec
{
    public const int ThumbnailMaxWidth = 320;
    public const int ThumbnailMaxHeight = 240;

    public static byte[] EncodePng(SKBitmap bitmap) => Encode(bitmap, SKEncodedImageFormat.Png, 100);

    public static byte[] EncodeJpeg(SKBitmap bitmap, int quality)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, 100);
        return Encode(bitmap, SKEncodedImageFormat.Jpeg, quality);
    }

    /// <summary>A WebP thumbnail that fits in 320x240, never upscaled.</summary>
    public static byte[] EncodeThumbnail(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var size = ThumbnailSize(new PixelSize(bitmap.Width, bitmap.Height));
        using var scaled = bitmap.Resize(
            new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Opaque),
            new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new InvalidOperationException("SkiaSharp could not scale the thumbnail.");
        return Encode(scaled, SKEncodedImageFormat.Webp, 80);
    }

    /// <summary>Fits a size inside the thumbnail box, keeping the aspect ratio, at least 1x1.</summary>
    public static PixelSize ThumbnailSize(PixelSize source)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(source.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(source.Height);

        var scale = Math.Min(1.0, Math.Min(
            (double)ThumbnailMaxWidth / source.Width,
            (double)ThumbnailMaxHeight / source.Height));
        return new PixelSize(
            Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(source.Height * scale)));
    }

    /// <summary>Decodes a PNG, JPG or WebP file into an opaque BGRA bitmap.</summary>
    public static SKBitmap Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        // SKBitmap.Decode throws ArgumentNullException (not null) for bytes no codec recognizes.
        using var skData = SKData.CreateCopy(data);
        using var codec = SKCodec.Create(skData) ?? throw new InvalidDataException("The data is not a PNG, JPG or WebP image.");
        using var decoded = SKBitmap.Decode(codec)
            ?? throw new InvalidDataException("The image could not be decoded.");
        var bitmap = new SKBitmap(new SKImageInfo(decoded.Width, decoded.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Black);
            using var image = SKImage.FromBitmap(decoded);
            canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);
        }

        return bitmap;
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality)
            ?? throw new InvalidOperationException($"SkiaSharp could not encode {format}.");
        return data.ToArray();
    }
}
