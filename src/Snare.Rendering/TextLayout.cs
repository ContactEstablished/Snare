using SkiaSharp;
using Snare.Core.Annotations;

namespace Snare.Rendering;

/// <summary>Measures and lays out a text annotation. Shared by the renderer and hit testing.</summary>
public sealed class TextLayout : IDisposable
{
    private TextLayout(SKFont font, string[] lines, float lineHeight, float ascent, float width, float padding)
    {
        Font = font;
        Lines = lines;
        LineHeight = lineHeight;
        Ascent = ascent;
        Width = width;
        Padding = padding;
    }

    public SKFont Font { get; }

    public IReadOnlyList<string> Lines { get; }

    public float LineHeight { get; }

    /// <summary>Distance from a line's top to its baseline.</summary>
    public float Ascent { get; }

    public float Width { get; }

    /// <summary>Space between the text and its background box.</summary>
    public float Padding { get; }

    public static TextLayout For(TextAnnotation text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var typeface = SKTypeface.FromFamilyName(text.FontFamily, text.Bold ? SKFontStyle.Bold : SKFontStyle.Normal)
            ?? SKTypeface.Default;
        var font = new SKFont(typeface, Math.Max(1, text.FontSize)) { Subpixel = true };
        var lines = text.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var metrics = font.Metrics;
        var width = lines.Length == 0 ? 0 : lines.Max(l => font.MeasureText(l));
        return new TextLayout(font, lines, font.Spacing, -metrics.Ascent, width, text.FontSize * 0.25f);
    }

    /// <summary>The text's box at its position, including the background padding.</summary>
    public SKRect BoundsAt(float x, float y) =>
        new(x - Padding, y - Padding, x + Math.Max(Width, LineHeight * 0.5f) + Padding, y + (LineHeight * Math.Max(1, Lines.Count)) + Padding);

    public void Dispose() => Font.Dispose();
}
