namespace Snare.Core.Search;

/// <summary>Colors given to new tags, in order; Matt can change a tag's color afterwards.</summary>
public static class TagPalette
{
    public static IReadOnlyList<string> Colors { get; } =
    [
        "#E53935", "#1E88E5", "#43A047", "#FB8C00", "#8E24AA",
        "#00ACC1", "#F4511E", "#3949AB", "#7CB342", "#D81B60",
    ];

    /// <summary>The color for the nth tag created (wrapping around the palette).</summary>
    public static string ForIndex(int index) => Colors[(int)((uint)index % Colors.Count)];

    /// <summary>The next palette color after <paramref name="current"/>, for click-to-cycle.</summary>
    public static string Next(string? current)
    {
        var index = Colors.ToList().FindIndex(c => string.Equals(c, current, StringComparison.OrdinalIgnoreCase));
        return ForIndex(index + 1);
    }
}
