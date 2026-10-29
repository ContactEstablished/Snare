using Snare.Core.Geometry;

namespace Snare.Core.Captures;

/// <summary>A top-level window as it was at the freeze. Bounds exclude invisible resize borders.</summary>
public sealed record WindowInfo(long Handle, string Title, string ProcessName, PixelRect Bounds);
