using SkiaSharp;
using Snare.Core.Imaging;

namespace Snare.Rendering;

/// <summary>Bridges Core's BGRA <see cref="PixelBuffer"/> and SkiaSharp images.</summary>
public static class PixelBufferImages
{
    public static SKImageInfo InfoFor(PixelBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return new SKImageInfo(buffer.Width, buffer.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
    }

    /// <summary>An immutable Skia image holding a copy of the buffer's pixels.</summary>
    public static SKImage ToImage(PixelBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return SKImage.FromPixelCopy(InfoFor(buffer), buffer.Pixels, buffer.Stride)
            ?? throw new InvalidOperationException("SkiaSharp could not copy the pixel buffer.");
    }

    /// <summary>A Skia bitmap holding a copy of the buffer's pixels, for drawing on.</summary>
    public static SKBitmap ToBitmap(PixelBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var bitmap = new SKBitmap(InfoFor(buffer));
        System.Runtime.InteropServices.Marshal.Copy(buffer.Pixels, 0, bitmap.GetPixels(), buffer.Pixels.Length);
        return bitmap;
    }
}
