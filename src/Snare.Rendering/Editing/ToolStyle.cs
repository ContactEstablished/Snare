using Snare.Core.Annotations;

namespace Snare.Rendering.Editing;

/// <summary>
/// The style a tool draws with. One shape covers every tool; each uses the fields that apply.
/// Each tool remembers its last style.
/// </summary>
/// <param name="Width">Stroke width; highlighter width; step badge radius.</param>
/// <param name="FillColor">Fill for rectangles and ellipses; null for none.</param>
/// <param name="TextBackground">Background box behind text; null for none.</param>
/// <param name="Strength">Blur sigma or pixelate block size.</param>
public sealed record ToolStyle(
    string Color,
    float Width,
    string? FillColor = null,
    float FontSize = 24,
    bool Bold = false,
    string? TextBackground = null,
    float Strength = RedactAnnotation.DefaultStrength,
    ArrowHeadStyle HeadStyle = ArrowHeadStyle.Filled,
    bool Dashed = false)
{
    public static ToolStyle DefaultFor(EditorTool tool) => tool switch
    {
        EditorTool.Arrow => new("#E53935", 6),
        EditorTool.Line => new("#E53935", 4),
        EditorTool.Rectangle or EditorTool.Ellipse => new("#E53935", 4),
        EditorTool.Highlighter => new("#FFEB3B", 18),
        EditorTool.Pen => new("#E53935", 4),
        EditorTool.Text => new("#E53935", 0, FontSize: 24),
        EditorTool.Redact => new("#000000", 0),
        EditorTool.Step => new("#E53935", 18),
        EditorTool.Callout => new("#E53935", 3, FillColor: "#FFFFFF", FontSize: 22),
        _ => new("#E53935", 4),
    };
}
