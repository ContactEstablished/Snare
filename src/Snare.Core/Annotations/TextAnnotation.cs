using Snare.Core.Geometry;

namespace Snare.Core.Annotations;

/// <summary>Text with its top-left at <paramref name="Position"/>. Lines are separated by '\n'.</summary>
public sealed record TextAnnotation(
    string Id,
    PixelPoint Position,
    string Text,
    string Color,
    float FontSize,
    string FontFamily = "Segoe UI",
    string? Background = null,
    bool Bold = false)
    : AnnotationObject(Id);
