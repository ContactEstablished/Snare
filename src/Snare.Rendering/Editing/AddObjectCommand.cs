using Snare.Core.Annotations;

namespace Snare.Rendering.Editing;

/// <summary>Adds an object at a z-order index (default: on top).</summary>
public sealed record AddObjectCommand(AnnotationObject Item, int? Index = null) : IEditCommand
{
    public string Name => "Add";

    public void Apply(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Objects.Insert(Math.Clamp(Index ?? document.Objects.Count, 0, document.Objects.Count), Item);
    }

    public void Revert(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Objects.RemoveAt(document.IndexOf(Item.Id));
    }
}
