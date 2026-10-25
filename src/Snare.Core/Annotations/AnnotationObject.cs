using System.Text.Json.Serialization;

namespace Snare.Core.Annotations;

/// <summary>Base type for every object the editor can draw. Add new tools as derived records here.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ArrowAnnotation), "arrow")]
[JsonDerivedType(typeof(RectangleAnnotation), "rectangle")]
[JsonDerivedType(typeof(RedactAnnotation), "redact")]
public abstract record AnnotationObject(string Id);
