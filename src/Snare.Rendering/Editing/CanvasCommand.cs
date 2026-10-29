using Snare.Core.Annotations;

namespace Snare.Rendering.Editing;

/// <summary>Crop and resize: whole-image operations, applied on export.</summary>
public sealed record CanvasCommand(string Name, CanvasInfo Before, CanvasInfo After) : IEditCommand
{
    public void Apply(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Canvas = After;
    }

    public void Revert(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Canvas = Before;
    }
}
