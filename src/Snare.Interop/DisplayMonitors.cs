using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace Snare.Interop;

public static class DisplayMonitors
{
    private const uint MonitorInfoPrimary = 1;
    private const uint DefaultDpi = 96;

    /// <summary>
    /// Every attached monitor in physical pixels. The calling process must be Per-Monitor V2
    /// DPI aware (Snare's manifest is), or Windows reports virtualized coordinates.
    /// </summary>
    public static unsafe IReadOnlyList<DisplayMonitor> GetAll()
    {
        var monitors = new List<DisplayMonitor>();

        BOOL Collect(HMONITOR handle, HDC dc, RECT* clip, LPARAM data)
        {
            MONITORINFOEXW info = default;
            info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
            if (!PInvoke.GetMonitorInfo(handle, (MONITORINFO*)&info))
            {
                return true;
            }

            var dpi = PInvoke.GetDpiForMonitor(handle, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _).Succeeded
                ? dpiX
                : DefaultDpi;

            monitors.Add(new DisplayMonitor(
                info.szDevice.ToString(),
                ToTuple(info.monitorInfo.rcMonitor),
                ToTuple(info.monitorInfo.rcWork),
                dpi,
                (info.monitorInfo.dwFlags & MonitorInfoPrimary) != 0));
            return true;
        }

        PInvoke.EnumDisplayMonitors(HDC.Null, (RECT?)null, Collect, default);
        return monitors;
    }

    private static (int X, int Y, int Width, int Height) ToTuple(RECT r) =>
        (r.left, r.top, r.right - r.left, r.bottom - r.top);
}
