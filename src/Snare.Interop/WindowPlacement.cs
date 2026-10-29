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

    /// <summary>The cursor position in physical virtual-desktop pixels.</summary>
    public static (int X, int Y) GetCursorPosition() =>
        PInvoke.GetCursorPos(out var point) ? (point.X, point.Y) : (0, 0);
}
