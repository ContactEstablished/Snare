using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace Snare.Interop;

public static class WindowSnapshot
{
    /// <summary>
    /// Every visible, uncloaked, non-minimized top-level window with a non-empty frame,
    /// in z-order from the top. Take this before showing any capture UI.
    /// </summary>
    public static unsafe IReadOnlyList<TopLevelWindow> TakeTopDown()
    {
        var windows = new List<TopLevelWindow>();

        BOOL Collect(HWND hwnd, LPARAM data)
        {
            if (!PInvoke.IsWindowVisible(hwnd) || PInvoke.IsIconic(hwnd) || IsCloaked(hwnd))
            {
                return true;
            }

            if (WindowBounds.GetExtendedFrameBounds(hwnd) is not { Width: > 0, Height: > 0 } bounds)
            {
                return true;
            }

            uint processId;
            PInvoke.GetWindowThreadProcessId(hwnd, &processId);
            windows.Add(new TopLevelWindow(hwnd, GetTitle(hwnd), processId, bounds));
            return true;
        }

        PInvoke.EnumWindows(Collect, default);
        return windows;
    }

    // Windows on other virtual desktops and suspended UWP frames are "cloaked": visible but not shown.
    private static unsafe bool IsCloaked(HWND hwnd)
    {
        uint cloaked = 0;
        var result = PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint));
        return result.Succeeded && cloaked != 0;
    }

    private static unsafe string GetTitle(HWND hwnd)
    {
        var length = PInvoke.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return "";
        }

        var buffer = new char[length + 1];
        fixed (char* text = buffer)
        {
            var copied = PInvoke.GetWindowText(hwnd, text, buffer.Length);
            return new string(buffer, 0, copied);
        }
    }
}
