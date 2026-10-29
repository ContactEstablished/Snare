using Snare.Core.Geometry;

namespace Snare.Core.Annotations;

public sealed record LineAnnotation(string Id, PixelPoint From, PixelPoint To, string Stroke, float StrokeWidth, bool Dashed = false)
    : AnnotationObject(Id);
