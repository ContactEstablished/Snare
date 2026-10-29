using System.Globalization;
using System.Text;
using Snare.Core.Captures;

namespace Snare.Core.Search;

/// <summary>
/// Turns the library search box into a <see cref="SearchQuery"/>. Filters: tag:, app:, kind:,
/// before:, after: (yyyy-MM-dd). Quotes group words. Anything unrecognized is a plain term.
/// </summary>
public static class SearchQueryParser
{
    public static SearchQuery Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return SearchQuery.All;
        }

        var terms = new List<string>();
        var tags = new List<string>();
        string? app = null;
        CaptureKind? kind = null;
        DateOnly? before = null;
        DateOnly? after = null;

        foreach (var token in Tokenize(input))
        {
            var colon = token.Quoted ? -1 : token.Text.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && colon < token.Text.Length - 1)
            {
                var key = token.Text[..colon].ToLowerInvariant();
                var value = token.Text[(colon + 1)..].Trim('"');
                switch (key)
                {
                    case "tag":
                        tags.Add(value);
                        continue;
                    case "app":
                        app = value;
                        continue;
                    case "kind" when Enum.TryParse<CaptureKind>(value, ignoreCase: true, out var k):
                        kind = k;
                        continue;
                    case "before" when TryDate(value, out var d):
                        before = d;
                        continue;
                    case "after" when TryDate(value, out var d):
                        after = d;
                        continue;
                }
            }

            terms.Add(token.Text);
        }

        return new SearchQuery { Terms = terms, Tags = tags, App = app, Kind = kind, Before = before, After = after };
    }

    private static bool TryDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>Splits on whitespace, keeping "quoted phrases" (and key:"quoted values") together.</summary>
    private static IEnumerable<(string Text, bool Quoted)> Tokenize(string input)
    {
        var current = new StringBuilder();
        var inQuotes = false;
        var quoted = false;

        foreach (var c in input)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                if (current.Length == 0)
                {
                    quoted = true;
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    yield return (Clean(current, quoted), quoted);
                }

                current.Clear();
                quoted = false;
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            yield return (Clean(current, quoted), quoted);
        }
    }

    private static string Clean(StringBuilder token, bool quoted) =>
        quoted ? token.ToString().TrimEnd('"') : token.ToString();
}
