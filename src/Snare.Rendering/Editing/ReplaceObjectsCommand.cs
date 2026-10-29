using Snare.Core.Annotations;

namespace Snare.Rendering.Editing;

/// <summary>Swaps objects for edited copies with the same ids: move, resize, restyle, edit text.</summary>
public sealed class ReplaceObjectsCommand(string name, IReadOnlyList<(AnnotationObject Before, AnnotationObject After)> changes)
    : IEditCommand
{
    public string Name { get; } = name;

    public IReadOnlyList<(AnnotationObject Before, AnnotationObject After)> Changes { get; } = changes;

    public void Apply(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        foreach (var (_, after) in Changes)
        {
            document.Objects[document.IndexOf(after.Id)] = after;
        }
    }

    public void Revert(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        foreach (var (before, _) in Changes)
        {
            document.Objects[document.IndexOf(before.Id)] = before;
        }
    }
}
