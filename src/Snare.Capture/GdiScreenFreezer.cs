using System.Diagnostics;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Imaging;
using Snare.Interop;

namespace Snare.Capture;

/// <summary>Freezes every monitor with one GDI BitBlt each, in physical pixels.</summary>
public sealed class GdiScreenFreezer(TimeProvider clock) : IScreenFreezer
{
    public FrozenDesktop Freeze(bool includeCursor = false)
    {
        // Windows and cursor first: they describe the moment of the hotkey, before any grab.
        var cursor = WindowPlacement.GetCursorPosition();
        var windows = SnapshotWindows();
        var capturedAt = clock.GetLocalNow();

        var frozen = DisplayMonitors.GetAll()
            .Select(m =>
            {
                var info = ToMonitorInfo(m);
                var b = info.Bounds;
                var pixels = ScreenGrabber.Grab(b.X, b.Y, b.Width, b.Height, includeCursor);
                return new FrozenMonitor(info, new PixelBuffer(b.Width, b.Height, pixels));
            })
            .ToList();

        return new FrozenDesktop(frozen, capturedAt, windows, new PixelPoint(cursor.X, cursor.Y));
    }

    internal static MonitorInfo ToMonitorInfo(DisplayMonitor m) => new(
        m.DeviceName,
        new PixelRect(m.Bounds.X, m.Bounds.Y, m.Bounds.Width, m.Bounds.Height),
        new PixelRect(m.WorkArea.X, m.WorkArea.Y, m.WorkArea.Width, m.WorkArea.Height),
        (int)m.Dpi,
        m.IsPrimary);

    /// <summary>Every other app's top-level window, topmost first. Snare's own windows are left out.</summary>
    private static List<WindowInfo> SnapshotWindows()
    {
        var ownProcess = (uint)Environment.ProcessId;
        var processNames = new Dictionary<uint, string>();

        return WindowSnapshot.TakeTopDown()
            .Where(w => w.ProcessId != ownProcess)
            .Select(w => new WindowInfo(
                w.Handle,
                w.Title,
                ProcessName(w.ProcessId, processNames),
                new PixelRect(w.Bounds.X, w.Bounds.Y, w.Bounds.Width, w.Bounds.Height)))
            .ToList();
    }

    private static string ProcessName(uint processId, Dictionary<uint, string> cache)
    {
        if (cache.TryGetValue(processId, out var name))
        {
            return name;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            name = process.ProcessName;
        }
        catch (ArgumentException)
        {
            name = ""; // exited between the snapshot and now
        }

        cache[processId] = name;
        return name;
    }
}
