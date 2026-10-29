using Snare.Core.Geometry;

namespace Snare.Core.Recording;

/// <summary>
/// What to record: an area on one display (physical virtual-desktop pixels), or a window.
/// <paramref name="Monitor"/> is the display's bounds; <paramref name="Area"/> lies inside it.
/// </summary>
public sealed record RecordingTarget(string DisplayDeviceName, PixelRect Monitor, PixelRect Area, long? WindowHandle = null)
{
    /// <summary>The area relative to its display's top-left, as the recorder wants it.</summary>
    public PixelRect AreaOnDisplay => Area with { X = Area.X - Monitor.X, Y = Area.Y - Monitor.Y };

    public bool IsWholeDisplay => WindowHandle is null && Area == Monitor;
}
