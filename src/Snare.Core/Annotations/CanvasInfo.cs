using Snare.Core.Geometry;

namespace Snare.Core.Annotations;

/// <summary>Size of the original capture and an optional crop applied on export.</summary>
public sealed record CanvasInfo(int Width, int Height, PixelRect? Crop = null);
