using Snare.Core.Geometry;

namespace Snare.Core.Annotations;

/// <summary>
/// A numbered badge. The number isn't stored: it is this badge's position among the step
/// badges in z-order, so deleting one renumbers the rest.
/// </summary>
public sealed record StepAnnotation(string Id, PixelPoint Center, string Fill, float Radius = 18)
    : AnnotationObject(Id);
