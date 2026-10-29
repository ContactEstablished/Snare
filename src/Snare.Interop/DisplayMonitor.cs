namespace Snare.Interop;

/// <summary>One monitor as Windows reports it. All rectangles are physical pixels on the virtual desktop.</summary>
public readonly record struct DisplayMonitor(
    string DeviceName,
    (int X, int Y, int Width, int Height) Bounds,
    (int X, int Y, int Width, int Height) WorkArea,
    uint Dpi,
    bool IsPrimary);
