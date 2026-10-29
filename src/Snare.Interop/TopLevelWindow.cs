namespace Snare.Interop;

/// <summary>A visible top-level window. Bounds are DWM's extended frame bounds in physical pixels.</summary>
public readonly record struct TopLevelWindow(
    nint Handle,
    string Title,
    uint ProcessId,
    (int X, int Y, int Width, int Height) Bounds);
