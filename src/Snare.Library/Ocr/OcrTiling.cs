using Snare.Core.Geometry;

namespace Snare.Library.Ocr;

/// <summary>
/// Windows OCR rejects images larger than its maximum dimension, so big captures (tall
/// scrolling ones especially) are read in overlapping tiles and the lines joined.
/// </summary>
internal static class OcrTiling
{
    public const int Overlap = 64;

    /// <summary>Tiles no larger than <paramref name="max"/> on either side, overlapping by <see cref="Overlap"/>.</summary>
    public static IReadOnlyList<PixelRect> Plan(int width, int height, int max)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(max, Overlap * 2);

        var tiles = new List<PixelRect>();
        foreach (var (y, h) in Spans(height, max))
        {
            foreach (var (x, w) in Spans(width, max))
            {
                tiles.Add(new PixelRect(x, y, w, h));
            }
        }

        return tiles;
    }

    /// <summary>
    /// Joins tiles' lines in order, dropping lines at the start of a tile that repeat the end of
    /// the previous one (text in the overlap is read twice).
    /// </summary>
    public static string JoinLines(IEnumerable<IReadOnlyList<string>> tiles)
    {
        var all = new List<string>();
        foreach (var lines in tiles)
        {
            var skip = 0;
            for (var n = Math.Min(3, Math.Min(lines.Count, all.Count)); n > 0; n--)
            {
                if (all.Skip(all.Count - n).SequenceEqual(lines.Take(n), StringComparer.Ordinal))
                {
                    skip = n;
                    break;
                }
            }

            all.AddRange(lines.Skip(skip));
        }

        return string.Join('\n', all.Where(l => l.Length > 0));
    }

    private static IEnumerable<(int Start, int Length)> Spans(int total, int max)
    {
        if (total <= max)
        {
            yield return (0, total);
            yield break;
        }

        var step = max - Overlap;
        for (var start = 0; ; start += step)
        {
            var length = Math.Min(max, total - start);
            yield return (start, length);
            if (start + length >= total)
            {
                yield break;
            }
        }
    }
}
