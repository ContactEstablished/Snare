namespace Snare.Rendering.Editing;

/// <summary>Single-letter tool shortcuts.</summary>
public static class EditorShortcuts
{
    public static IReadOnlyDictionary<char, EditorTool> ByKey { get; } = new Dictionary<char, EditorTool>
    {
        ['V'] = EditorTool.Select,
        ['A'] = EditorTool.Arrow,
        ['L'] = EditorTool.Line,
        ['R'] = EditorTool.Rectangle,
        ['E'] = EditorTool.Ellipse,
        ['H'] = EditorTool.Highlighter,
        ['P'] = EditorTool.Pen,
        ['T'] = EditorTool.Text,
        ['D'] = EditorTool.Redact,
        ['B'] = EditorTool.Blur,
        ['X'] = EditorTool.Pixelate,
        ['N'] = EditorTool.Step,
        ['I'] = EditorTool.Eyedropper,
        ['C'] = EditorTool.Crop,
    };

    public static char KeyFor(EditorTool tool) => ByKey.First(pair => pair.Value == tool).Key;
}
