using Snare.Core.Geometry;

namespace Snare.Core.Annotations;

/// <summary>A translucent marker stroke, blended with multiply so the text underneath stays readable.</summary>
public sealed record HighlighterAnnotation(string Id, IReadOnlyList<PixelPoint> Points, string Color, float Width)
    : AnnotationObject(Id)
{
    // Records compare lists by reference; compare points by value instead.
    public bool Equals(HighlighterAnnotation? other) =>
        other is not null && Id == other.Id && Color == other.Color && Width.Equals(other.Width)
        && Points.SequenceEqual(other.Points);

    public override int GetHashCode() => HashCode.Combine(Id, Color, Width, Points.Count);
}
