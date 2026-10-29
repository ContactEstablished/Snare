namespace Snare.Core.Search;

/// <summary>A tag, its color, and how many captures (not in the Trash) use it.</summary>
public sealed record TagInfo(string Name, string Color, int Count);
