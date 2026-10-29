using Snare.Core.Geometry;

namespace Snare.Core.Annotations;

/// <summary>A freehand stroke. Points are raw input; smoothing happens when it is drawn.</summary>
public sealed record PenAnnotation(string Id, IReadOnlyList<PixelPoint> Points, string Stroke, float StrokeWidth)
    : AnnotationObject(Id)
{
    // Records compare lists by reference; compare points by value instead.
    public bool Equals(PenAnnotation? other) =>
        other is not null && Id == other.Id && Stroke == other.Stroke && StrokeWidth.Equals(other.StrokeWidth)
        && Points.SequenceEqual(other.Points);

    public override int GetHashCode() => HashCode.Combine(Id, Stroke, StrokeWidth, Points.Count);
}
