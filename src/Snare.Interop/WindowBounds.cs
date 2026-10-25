using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace Snare.Interop;

public static class WindowBounds
{
    /// <summary>
    /// The window's visible bounds in physical pixels, excluding the invisible resize borders
    /// that GetWindowRect includes. Returns null if DWM can't answer for this window.
    /// </summary>
    public static unsafe (int X, int Y, int Width, int Height)? GetExtendedFrameBounds(nint windowHandle)
    {
        RECT rect;
        var result = PInvoke.DwmGetWindowAttribute(
            (HWND)windowHandle,
            DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
            &rect,
            (uint)sizeof(RECT));

        if (result.Failed)
        {
            return null;
        }

        return (rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
    }
}
