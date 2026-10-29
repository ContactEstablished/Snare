using Snare.Core.Geometry;

namespace Snare.Core.Annotations;

public sealed record EllipseAnnotation(string Id, PixelRect Bounds, string Stroke, float StrokeWidth, string? Fill = null)
    : AnnotationObject(Id);
