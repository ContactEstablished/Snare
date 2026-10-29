using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Snare.Interop;

public static class WindowPlacement
{
    private static readonly HWND TopMost = new(-1);

    /// <summary>
    /// Places a window at exact physical pixels and keeps it on top. WPF's Left/Top are DIPs
    /// relative to the primary monitor's DPI, which drifts on mixed-DPI layouts; this doesn't.
    /// </summary>
    public static bool SetTopmostPhysicalBounds(nint windowHandle, int x, int y, int width, int height) =>
        PInvoke.SetWindowPos((HWND)windowHandle, TopMost, x, y, width, height,
            SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);

    /// <summary>Moves a window's top-left to a physical point, keeping its size, on top, without activating it.</summary>
    public static bool MoveTopmost(nint windowHandle, int x, int y) =>
        PInvoke.SetWindowPos((HWND)windowHandle, TopMost, x, y, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

    /// <summary>
    /// Stops clicks on a window from activating it (WS_EX_NOACTIVATE), so a floating panel's
    /// buttons don't take keyboard focus away from the app Matt is working in.
    /// </summary>
    public static void MakeNoActivate(nint windowHandle)
    {
        var style = PInvoke.GetWindowLongPtr((HWND)windowHandle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        PInvoke.SetWindowLongPtr((HWND)windowHandle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, style | (nint)WINDOW_EX_STYLE.WS_EX_NOACTIVATE);
    }

    /// <summary>The cursor position in physical virtual-desktop pixels.</summary>
    public static (int X, int Y) GetCursorPosition() =>
        PInvoke.GetCursorPos(out var point) ? (point.X, point.Y) : (0, 0);
}
