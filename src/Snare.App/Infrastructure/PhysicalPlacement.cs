using System.Windows;
using System.Windows.Interop;
using Snare.Core.Geometry;
using Snare.Interop;

namespace Snare.App.Infrastructure;

/// <summary>
/// Pins a borderless window to exact physical pixels. WPF's Left/Top/Width/Height are DIPs and
/// drift on mixed-DPI layouts, and WM_DPICHANGED would resize the window; this re-applies the
/// bounds whenever that happens.
/// </summary>
internal static class PhysicalPlacement
{
    private const int WmDpiChanged = 0x02E0;

    public static void Pin(Window window, PixelRect bounds)
    {
        ArgumentNullException.ThrowIfNull(window);

        void Apply() => WindowPlacement.SetTopmostPhysicalBounds(
            new WindowInteropHelper(window).Handle, bounds.X, bounds.Y, bounds.Width, bounds.Height);

        nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
        {
            if (msg == WmDpiChanged)
            {
                Apply();
                handled = true;
            }

            return 0;
        }

        window.SourceInitialized += (_, _) =>
        {
            HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)?.AddHook(Hook);
            Apply();
        };
    }

    /// <summary>A physical point on the virtual desktop, in DIPs relative to a monitor's top-left.</summary>
    public static Point ToLocalDip(PixelPoint point, PixelRect monitor, double scale) =>
        new((point.X - monitor.X) / scale, (point.Y - monitor.Y) / scale);

    public static Rect ToLocalDip(PixelRect rect, PixelRect monitor, double scale) =>
        new((rect.X - monitor.X) / scale, (rect.Y - monitor.Y) / scale, rect.Width / scale, rect.Height / scale);
}
