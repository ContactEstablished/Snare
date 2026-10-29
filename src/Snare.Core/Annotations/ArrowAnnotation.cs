using Snare.Core.Geometry;

namespace Snare.Core.Annotations;

/// <param name="HeadSize">Length of the head as a multiple of <paramref name="StrokeWidth"/>.</param>
public sealed record ArrowAnnotation(
    string Id,
    PixelPoint From,
    PixelPoint To,
    string Stroke,
    float StrokeWidth,
    ArrowHeadStyle HeadStyle = ArrowHeadStyle.Filled,
    float HeadSize = 4)
    : AnnotationObject(Id);
