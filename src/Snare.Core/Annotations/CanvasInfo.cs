using System.Text.Json.Serialization;
using Snare.Core.Geometry;

namespace Snare.Core.Annotations;

/// <summary>
/// Size of the original capture, plus whole-image operations applied on export:
/// <paramref name="Crop"/> (in original pixels), then <paramref name="Resize"/> (final output size).
/// </summary>
public sealed record CanvasInfo(int Width, int Height, PixelRect? Crop = null, PixelSize? Resize = null)
{
    /// <summary>The part of the original that is exported.</summary>
    [JsonIgnore]
    public PixelRect ExportArea => Crop?.Intersect(new PixelRect(0, 0, Width, Height)) is { IsEmpty: false } crop
        ? crop
        : new PixelRect(0, 0, Width, Height);

    /// <summary>The exported image's size.</summary>
    [JsonIgnore]
    public PixelSize OutputSize => Resize ?? new PixelSize(ExportArea.Width, ExportArea.Height);
}
