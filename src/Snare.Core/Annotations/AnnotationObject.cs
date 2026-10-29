using System.Text.Json.Serialization;

namespace Snare.Core.Annotations;

/// <summary>Base type for every object the editor can draw. Add new tools as derived records here.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ArrowAnnotation), "arrow")]
[JsonDerivedType(typeof(LineAnnotation), "line")]
[JsonDerivedType(typeof(RectangleAnnotation), "rectangle")]
[JsonDerivedType(typeof(EllipseAnnotation), "ellipse")]
[JsonDerivedType(typeof(PenAnnotation), "pen")]
[JsonDerivedType(typeof(HighlighterAnnotation), "highlighter")]
[JsonDerivedType(typeof(TextAnnotation), "text")]
[JsonDerivedType(typeof(RedactAnnotation), "redact")]
[JsonDerivedType(typeof(StepAnnotation), "step")]
public abstract record AnnotationObject(string Id);
