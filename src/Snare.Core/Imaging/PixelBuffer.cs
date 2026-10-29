namespace Snare.Core.Imaging;

/// <summary>
/// An image as top-down 32-bit BGRA bytes with no row padding (stride = width * 4).
/// This is the format GDI grabs produce and WPF and SkiaSharp both read directly.
/// </summary>
public sealed class PixelBuffer
{
    public const int BytesPerPixel = 4;

    public PixelBuffer(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != (long)width * height * BytesPerPixel)
        {
            throw new ArgumentException(
                $"Expected {(long)width * height * BytesPerPixel} bytes for {width}x{height}, got {pixels.Length}.",
                nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride => Width * BytesPerPixel;

    public byte[] Pixels { get; }

    /// <summary>A new buffer filled with one opaque color.</summary>
    public static PixelBuffer Filled(int width, int height, uint argb)
    {
        var buffer = new PixelBuffer(width, height, new byte[width * height * BytesPerPixel]);
        var span = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(buffer.Pixels.AsSpan());
        span.Fill(argb); // little-endian uint 0xAARRGGBB is stored as B, G, R, A
        return buffer;
    }

    /// <summary>The pixel at (x, y) as 0xAARRGGBB.</summary>
    public uint GetArgb(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is outside {Width}x{Height}.");
        }

        var i = (y * Stride) + (x * BytesPerPixel);
        return (uint)(Pixels[i + 3] << 24 | Pixels[i + 2] << 16 | Pixels[i + 1] << 8 | Pixels[i]);
    }
}
