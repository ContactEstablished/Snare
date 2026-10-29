using Snare.Core.Annotations;

namespace Snare.Rendering.Editing;

/// <summary>The mutable state commands act on: the canvas and objects in z-order (first is bottom).</summary>
public sealed class EditorDocument
{
    public EditorDocument(AnnotationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Canvas = document.Canvas;
        Objects = [.. document.Objects];
    }

    public CanvasInfo Canvas { get; set; }

    public List<AnnotationObject> Objects { get; }

    public int IndexOf(string id) => Objects.FindIndex(o => o.Id == id);

    public AnnotationObject? Find(string id) => Objects.Find(o => o.Id == id);

    /// <summary>A snapshot for saving and rendering.</summary>
    public AnnotationDocument ToAnnotationDocument() => new() { Canvas = Canvas, Objects = [.. Objects] };
}
