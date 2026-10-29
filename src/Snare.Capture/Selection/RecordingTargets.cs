using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Recording;

namespace Snare.Capture.Selection;

/// <summary>Turns an overlay selection into something one recording can cover: a single display.</summary>
public static class RecordingTargets
{
    /// <summary>
    /// The display under the selection's center, with the area clipped to it. Null for
    /// "all monitors" or a selection off every display, which a recording can't span.
    /// </summary>
    public static RecordingTarget? From(SelectionResult selection, FrozenDesktop desktop)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(desktop);
        if (selection.Target == CaptureTarget.AllMonitors)
        {
            return null;
        }

        var rect = selection.Rect;
        var center = new PixelPoint(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
        var monitor = desktop.MonitorAt(center)?.Monitor
            ?? desktop.Monitors.Select(m => m.Monitor).OrderByDescending(m => Overlap(m.Bounds, rect)).FirstOrDefault(m => Overlap(m.Bounds, rect) > 0);
        if (monitor is null)
        {
            return null;
        }

        var area = rect.Intersect(monitor.Bounds);
        if (area.Width < 2 || area.Height < 2)
        {
            return null;
        }

        return new RecordingTarget(monitor.DeviceName, monitor.Bounds, area, selection.Window?.Handle);
    }

    private static long Overlap(PixelRect a, PixelRect b)
    {
        var i = a.Intersect(b);
        return i.IsEmpty ? 0 : (long)i.Width * i.Height;
    }
}
