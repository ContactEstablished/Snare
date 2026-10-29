using SkiaSharp;

namespace Snare.Rendering;

/// <summary>Annotation colors are "#RRGGBB" or "#AARRGGBB" strings.</summary>
public static class SkiaColors
{
    public static SKColor Parse(string? value, SKColor fallback) =>
        value is not null && SKColor.TryParse(value, out var color) ? color : fallback;

    public static string ToHex(SKColor color) => color.Alpha == 0xFF
        ? $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}"
        : $"#{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
}
