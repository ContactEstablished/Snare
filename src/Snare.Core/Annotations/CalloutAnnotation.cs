using Snare.Core.Geometry;

namespace Snare.Core.Annotations;

/// <summary>
/// A rounded text bubble in <paramref name="Bounds"/> with a tail pointing at <paramref name="Tail"/>
/// (no tail when the point is inside the bubble). Lines of text are separated by '\n'.
/// </summary>
public sealed record CalloutAnnotation(
    string Id,
    PixelRect Bounds,
    PixelPoint Tail,
    string Text,
    string Color,
    float FontSize,
    string Fill = "#FFFFFF",
    float StrokeWidth = 3,
    bool Bold = false)
    : AnnotationObject(Id)
{
    /// <summary>Space between the bubble's edge and its text.</summary>
    public float Padding => Math.Max(6, FontSize * 0.5f);
}
