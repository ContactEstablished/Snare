using Snare.Core.Geometry;

namespace Snare.Core.Captures;

/// <summary>Every monitor frozen at once. Selection happens on this, never on the live screen.</summary>
public sealed class FrozenDesktop
{
    public FrozenDesktop(IReadOnlyList<FrozenMonitor> monitors, DateTimeOffset capturedAt)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        if (monitors.Count == 0)
        {
            throw new ArgumentException("At least one monitor is required.", nameof(monitors));
        }

        foreach (var m in monitors)
        {
            if (m.Frame.Width != m.Monitor.Bounds.Width || m.Frame.Height != m.Monitor.Bounds.Height)
            {
                throw new ArgumentException($"Frame for {m.Monitor.DeviceName} doesn't match its bounds.", nameof(monitors));
            }
        }

        Monitors = monitors;
        CapturedAt = capturedAt;
        Bounds = PixelRect.Union(monitors.Select(m => m.Monitor.Bounds));
    }

    public IReadOnlyList<FrozenMonitor> Monitors { get; }

    public DateTimeOffset CapturedAt { get; }

    /// <summary>The bounding box of every monitor. Parts of it may belong to no monitor.</summary>
    public PixelRect Bounds { get; }

    public FrozenMonitor? MonitorAt(PixelPoint point) =>
        Monitors.FirstOrDefault(m => m.Monitor.Bounds.Contains(point));

    /// <summary>The frozen pixel at a virtual-desktop point as 0xAARRGGBB, or null off every monitor.</summary>
    public uint? GetArgb(PixelPoint point)
    {
        var monitor = MonitorAt(point);
        return monitor?.Frame.GetArgb(point.X - monitor.Monitor.Bounds.X, point.Y - monitor.Monitor.Bounds.Y);
    }
}
