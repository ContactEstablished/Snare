namespace Snare.Core.Annotations;

/// <summary>
/// The editable layer for one capture: vector objects drawn over the untouched original.
/// Stored as JSON in the library database; the exported PNG/JPG is always flattened.
/// </summary>
public sealed class AnnotationDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required CanvasInfo Canvas { get; init; }

    /// <summary>Objects in z-order: the first is drawn first (bottom).</summary>
    public IList<AnnotationObject> Objects { get; init; } = new List<AnnotationObject>();
}
