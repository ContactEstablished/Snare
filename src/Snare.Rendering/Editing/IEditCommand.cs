namespace Snare.Rendering.Editing;

/// <summary>One undoable change to the editor's document.</summary>
public interface IEditCommand
{
    string Name { get; }

    void Apply(EditorDocument document);

    void Revert(EditorDocument document);
}
