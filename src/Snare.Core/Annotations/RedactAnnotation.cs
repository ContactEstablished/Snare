using System.Text.Json.Serialization;
using Snare.Core.Geometry;

namespace Snare.Core.Annotations;

/// <summary>
/// Hides part of the image. Only <see cref="RedactMode.Solid"/> is secure;
/// blurred or pixelated text can sometimes be recovered.
/// </summary>
/// <param name="Strength">Blur sigma or pixelate block size in pixels; 0 means the default.</param>
public sealed record RedactAnnotation(string Id, PixelRect Bounds, RedactMode Mode, string Fill = "#000000", float Strength = 0)
    : AnnotationObject(Id)
{
    public const float DefaultStrength = 12;

    [JsonIgnore]
    public float EffectiveStrength => Strength > 0 ? Strength : DefaultStrength;
}
