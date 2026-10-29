using Snare.Core.Captures;

namespace Snare.Core.Search;

/// <summary>A parsed library search: free terms plus filters. An empty query lists everything.</summary>
public sealed record SearchQuery
{
    public static SearchQuery All { get; } = new();

    /// <summary>Words or quoted phrases matched against the full-text index (prefix matching).</summary>
    public IReadOnlyList<string> Terms { get; init; } = [];

    /// <summary>Every tag listed must be on the capture.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Source app name prefix, case-insensitive.</summary>
    public string? App { get; init; }

    public CaptureKind? Kind { get; init; }

    /// <summary>Only captures before the start of this local date.</summary>
    public DateOnly? Before { get; init; }

    /// <summary>Only captures on or after this local date.</summary>
    public DateOnly? After { get; init; }

    public bool HasTerms => Terms.Count > 0;

    /// <summary>
    /// The terms as an FTS5 expression: each term quoted (quotes doubled) and made a prefix,
    /// all AND-ed, so "invoice err" finds "invoices" and "error". Null with no terms.
    /// </summary>
    public string? ToFtsExpression() => HasTerms
        ? string.Join(' ', Terms.Select(t => $"\"{t.Replace("\"", "\"\"", StringComparison.Ordinal)}\"*"))
        : null;
}
