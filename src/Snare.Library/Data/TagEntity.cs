namespace Snare.Library.Data;

/// <summary>EF Core row for the Tag table. Names are unique, ignoring case.</summary>
internal sealed class TagEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? Color { get; set; }
}
