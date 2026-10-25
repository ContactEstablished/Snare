using Snare.Core.Geometry;

namespace Snare.Core.Annotations;

/// <summary>
/// Hides part of the image. Only <see cref="RedactMode.Solid"/> is secure;
/// blurred or pixelated text can sometimes be recovered.
/// </summary>
public sealed record RedactAnnotation(string Id, PixelRect Bounds, RedactMode Mode, string Fill = "#000000")
    : AnnotationObject(Id);
