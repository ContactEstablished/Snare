using Snare.Core.Geometry;

namespace Snare.Core.Annotations;

public sealed record ArrowAnnotation(string Id, PixelPoint From, PixelPoint To, string Stroke, float StrokeWidth)
    : AnnotationObject(Id);
