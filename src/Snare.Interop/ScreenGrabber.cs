using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Snare.Interop;

public static class ScreenGrabber
{
    /// <summary>
    /// Copies a rectangle of the screen, in physical virtual-desktop pixels, into a top-down
    /// 32-bit BGRA buffer (stride = width * 4). CAPTUREBLT includes layered windows such as
    /// menus and tooltips, which is what the freeze-first capture exists for.
    /// </summary>
    public static unsafe byte[] Grab(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var byteCount = (long)width * height * 4;
        if (byteCount > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(width), $"A {width}x{height} grab is too large for one buffer.");
        }

        var screenDc = PInvoke.GetDC(HWND.Null);
        if (screenDc.IsNull)
        {
            throw new InvalidOperationException("GetDC failed for the screen.");
        }

        var memoryDc = PInvoke.CreateCompatibleDC(screenDc);
        var bitmap = PInvoke.CreateCompatibleBitmap(screenDc, width, height);
        try
        {
            if (memoryDc.IsNull || bitmap.IsNull)
            {
                throw new InvalidOperationException("Could not create a GDI bitmap for the grab.");
            }

            var previous = PInvoke.SelectObject(memoryDc, bitmap);
            var copied = PInvoke.BitBlt(memoryDc, 0, 0, width, height, screenDc, x, y,
                ROP_CODE.SRCCOPY | ROP_CODE.CAPTUREBLT);
            PInvoke.SelectObject(memoryDc, previous);
            if (!copied)
            {
                throw new InvalidOperationException("BitBlt failed.");
            }

            var pixels = new byte[byteCount];
            var header = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)sizeof(BITMAPINFOHEADER),
                    biWidth = width,
                    biHeight = -height, // negative: top-down rows
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0, // BI_RGB
                },
            };

            fixed (byte* bits = pixels)
            {
                var lines = PInvoke.GetDIBits(memoryDc, bitmap, 0, (uint)height, bits, &header, DIB_USAGE.DIB_RGB_COLORS);
                if (lines != height)
                {
                    throw new InvalidOperationException("GetDIBits returned fewer lines than requested.");
                }
            }

            // BitBlt leaves alpha undefined; captures are opaque.
            for (var i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 0xFF;
            }

            return pixels;
        }
        finally
        {
            if (!bitmap.IsNull)
            {
                PInvoke.DeleteObject(bitmap);
            }

            if (!memoryDc.IsNull)
            {
                PInvoke.DeleteDC(memoryDc);
            }

            PInvoke.ReleaseDC(HWND.Null, screenDc);
        }
    }
}
