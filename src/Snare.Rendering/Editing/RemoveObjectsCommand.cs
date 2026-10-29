using Snare.Core.Annotations;

namespace Snare.Rendering.Editing;

/// <summary>Removes objects, remembering where each was so undo restores the z-order.</summary>
public sealed class RemoveObjectsCommand(IEnumerable<string> ids) : IEditCommand
{
    private readonly HashSet<string> _ids = [.. ids];
    private List<(int Index, AnnotationObject Item)> _removed = [];

    public string Name => "Delete";

    public void Apply(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _removed = document.Objects
            .Select((item, index) => (index, item))
            .Where(pair => _ids.Contains(pair.item.Id))
            .ToList();
        document.Objects.RemoveAll(o => _ids.Contains(o.Id));
    }

    public void Revert(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        foreach (var (index, item) in _removed)
        {
            document.Objects.Insert(index, item);
        }
    }
}
