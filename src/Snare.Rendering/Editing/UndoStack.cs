namespace Snare.Rendering.Editing;

/// <summary>Unlimited undo and redo for one editing session.</summary>
public sealed class UndoStack(EditorDocument document)
{
    private readonly Stack<IEditCommand> _undo = new();
    private readonly Stack<IEditCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Applies a command and makes it undoable.</summary>
    public void Execute(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Apply(document);
        Record(command);
    }

    /// <summary>Records a command whose effect is already in the document (a finished drag).</summary>
    public void Record(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _undo.Push(command);
        _redo.Clear();
    }

    public IEditCommand? Undo()
    {
        if (!_undo.TryPop(out var command))
        {
            return null;
        }

        command.Revert(document);
        _redo.Push(command);
        return command;
    }

    public IEditCommand? Redo()
    {
        if (!_redo.TryPop(out var command))
        {
            return null;
        }

        command.Apply(document);
        _undo.Push(command);
        return command;
    }
}
