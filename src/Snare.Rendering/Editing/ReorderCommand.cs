namespace Snare.Rendering.Editing;

/// <summary>Changes z-order by swapping the whole id order.</summary>
public sealed class ReorderCommand(IReadOnlyList<string> before, IReadOnlyList<string> after) : IEditCommand
{
    public string Name => "Arrange";

    public void Apply(EditorDocument document) => Order(document, after);

    public void Revert(EditorDocument document) => Order(document, before);

    private static void Order(EditorDocument document, IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(document);
        var byId = document.Objects.ToDictionary(o => o.Id);
        document.Objects.Clear();
        document.Objects.AddRange(ids.Select(id => byId[id]));
    }
}
