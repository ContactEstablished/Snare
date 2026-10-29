using Snare.Core.Geometry;

namespace Snare.Core.Captures;

/// <summary>A monitor on the virtual desktop. Bounds are physical pixels; Dpi is 96 at 100% scaling.</summary>
public sealed record MonitorInfo(string DeviceName, PixelRect Bounds, PixelRect WorkArea, int Dpi, bool IsPrimary)
{
    public double Scale => Dpi / 96.0;
}
