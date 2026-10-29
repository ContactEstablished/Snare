using OpenCvSharp;
using Snare.Core.Imaging;

namespace Snare.Capture.Scrolling;

/// <summary>
/// Stitches same-size frames of a region scrolling downward into one tall image.
/// </summary>
/// <remarks>
/// Each new frame is lined up with the previous one by finding a textured strip from the bottom of
/// the previous frame's content in the new frame. Rows identical at the same position in both
/// frames, counted from the top and bottom, are a sticky header and footer: they are left out of
/// matching and kept once (header from the first frame, footer from the last). Only rows that can
/// no longer change are committed, so a wrong sticky guess never loses rows.
/// </remarks>
public sealed class ScrollStitcher
{
    public const int DefaultMaxHeight = 30_000;

    /// <summary>Normalized cross-correlation a match needs.</summary>
    public const double MinConfidence = 0.9;

    /// <summary>Largest share of the frame a sticky header or footer can take.</summary>
    private const double MaxStickyShare = 0.4;

    /// <summary>A pixel counts as changed when its gray value moves by more than this (grab noise is 1).</summary>
    private const int PixelTolerance = 2;

    /// <summary>A row is unchanged when at most this share of its scrolling columns changed.</summary>
    private const double StickyRowChangeShare = 0.05;

    /// <summary>Unchanged columns narrower than this inside the scrolling content are bridged.</summary>
    private const int MaxColumnGap = 8;

    /// <summary>The largest mean gray difference allowed over the whole overlap of a match.</summary>
    private const double OverlapTolerance = 6.0;

    /// <summary>A template strip needs at least this <see cref="EdgeStrength"/> to be worth matching.</summary>
    private const double MinEdgeStrength = 2.0;

    private const int MinBand = 32;

    private readonly List<byte[]> _committed = [];
    private PixelBuffer? _previous;
    private byte[]? _previousGray;
    private int _committedRows;
    private int _pendingTop; // first row of _previous not yet committed

    public ScrollStitcher(int maxHeight = DefaultMaxHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHeight);
        MaxHeight = maxHeight;
    }

    public int MaxHeight { get; }

    public int Width => _previous?.Width ?? 0;

    /// <summary>The height the image would have if it were composed now (capped).</summary>
    public int Height => _previous is null ? 0 : Math.Min(MaxHeight, _committedRows + _previous.Height - _pendingTop);

    public bool IsFull { get; private set; }

    /// <summary>Lines up a frame with the previous one and adds the rows it scrolled into view.</summary>
    public StitchResult Add(PixelBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (_previous is null || _previousGray is null)
        {
            _previous = frame;
            _previousGray = ToGray(frame);
            IsFull = frame.Height >= MaxHeight;
            return new StitchResult(IsFull ? StitchOutcome.LimitReached : StitchOutcome.Started);
        }

        if (frame.Width != _previous.Width || frame.Height != _previous.Height)
        {
            throw new ArgumentException(
                $"Frames must all be {_previous.Width}x{_previous.Height}; got {frame.Width}x{frame.Height}.", nameof(frame));
        }

        if (IsFull)
        {
            return new StitchResult(StitchOutcome.LimitReached);
        }

        var gray = ToGray(frame);
        var match = Match(_previousGray, gray, frame.Width, frame.Height);
        if (match.Result.Outcome != StitchOutcome.Appended)
        {
            return match.Result;
        }

        // Commit the previous frame down to the top of its footer, then continue from the same
        // row in the new frame. The rows below are kept pending until a later frame or Compose.
        var height = frame.Height;
        var end = Math.Max(_pendingTop, height - match.Footer);
        Commit(_previous, _pendingTop, end);
        _pendingTop = end - match.Result.Offset;
        _previous = frame;
        _previousGray = gray;

        if (_committedRows + height - _pendingTop >= MaxHeight)
        {
            IsFull = true;
            return match.Result with { Outcome = StitchOutcome.LimitReached };
        }

        return match.Result;
    }

    /// <summary>The image so far: everything committed plus the rest of the latest frame, capped at <see cref="MaxHeight"/>.</summary>
    public PixelBuffer Compose()
    {
        var previous = _previous ?? throw new InvalidOperationException("No frames were added.");
        var height = Height;
        var stride = previous.Stride;
        var pixels = new byte[PixelBuffer.ByteCount(previous.Width, height)];
        var offset = 0;
        foreach (var chunk in _committed)
        {
            var length = Math.Min(chunk.Length, pixels.Length - offset);
            chunk.AsSpan(0, length).CopyTo(pixels.AsSpan(offset));
            offset += length;
        }

        var tail = Math.Min(pixels.Length - offset, (previous.Height - _pendingTop) * stride);
        previous.Pixels.AsSpan(_pendingTop * stride, tail).CopyTo(pixels.AsSpan(offset));
        return new PixelBuffer(previous.Width, height, pixels);
    }

    /// <summary>The bottom <paramref name="rows"/> of the image so far, for the live preview.</summary>
    public PixelBuffer Tail(int rows)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        var previous = _previous ?? throw new InvalidOperationException("No frames were added.");
        var height = Math.Min(rows, Height);
        var stride = previous.Stride;
        var pixels = new byte[PixelBuffer.ByteCount(previous.Width, height)];

        // Fill from the bottom up: the pending rows of the latest frame, then committed chunks.
        var remaining = height;
        var fromFrame = Math.Min(remaining, Math.Max(0, Height - _committedRows));
        previous.Pixels.AsSpan((_pendingTop + Height - _committedRows - fromFrame) * stride, fromFrame * stride).CopyTo(pixels.AsSpan((height - fromFrame) * stride));
        remaining -= fromFrame;
        for (var i = _committed.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var chunk = _committed[i];
            var chunkRows = chunk.Length / stride;
            var take = Math.Min(remaining, chunkRows);
            chunk.AsSpan((chunkRows - take) * stride, take * stride).CopyTo(pixels.AsSpan((remaining - take) * stride));
            remaining -= take;
        }

        return new PixelBuffer(previous.Width, height, pixels);
    }

    private void Commit(PixelBuffer frame, int fromRow, int toRow)
    {
        if (toRow <= fromRow)
        {
            return;
        }

        var stride = frame.Stride;
        _committed.Add(frame.Pixels.AsSpan(fromRow * stride, (toRow - fromRow) * stride).ToArray());
        _committedRows += toRow - fromRow;
    }

    private static (StitchResult Result, int Footer) Match(byte[] previous, byte[] current, int width, int height)
    {
        StitchResult Drop(StitchOutcome outcome, string? warning = null, double confidence = 0) => new(outcome, 0, confidence, warning);

        // Only the columns that changed take part: a fixed sidebar, a table of contents or the
        // window's frame stays put and would otherwise pull every match toward "no movement".
        if (ChangedColumns(previous, current, width, height) is not { } columns)
        {
            return (Drop(StitchOutcome.NoMovement), 0);
        }

        var (header, footer, allSame) = StickyRows(previous, current, width, height, columns);
        if (allSame)
        {
            return (Drop(StitchOutcome.NoMovement), 0);
        }

        var band = new Band(width, header, height - footer, columns.Left, columns.Right);
        if (band.Height < MinBand)
        {
            return (Drop(StitchOutcome.NoMatch, "Too little of the area scrolls to line up frames."), 0);
        }

        // Small, so a blank stretch at the bottom of the view costs little range; the overlap check
        // below catches the odd false match a small strip allows.
        var strip = Math.Clamp(band.Height / 8, 16, 48);
        var templateTop = TexturedStrip(previous, current, band, strip, fromBottom: true);
        if (templateTop is not { } top)
        {
            return (Drop(StitchOutcome.NoMatch, "Nothing to line up in this part of the page; keep scrolling."), 0);
        }

        var (foundTop, confidence) = FindStrip(previous, current, band, top, strip);
        var offset = top - foundTop;
        if (confidence < MinConfidence
            && TexturedStrip(previous, current, band, strip, fromBottom: false) is { } upper)
        {
            // The bottom strip leaves the view when the page scrolls up; one from the top doesn't.
            var (upperFound, upperConfidence) = FindStrip(previous, current, band, upper, strip);
            if (upperConfidence >= MinConfidence)
            {
                (offset, confidence) = (upper - upperFound, upperConfidence);
            }
        }

        if (confidence < MinConfidence)
        {
            return (Drop(StitchOutcome.NoMatch, "Couldn't line up that frame; scroll a little slower.", confidence), 0);
        }

        // The strip can match a row or two off where the page is smooth; the whole overlap can't.
        var difference = double.MaxValue;
        var best = offset;
        for (var candidate = offset - 2; candidate <= offset + 2; candidate++)
        {
            var d = OverlapDifference(previous, current, band, candidate);
            if (d < difference)
            {
                (difference, best) = (d, candidate);
            }
        }

        if (best == 0)
        {
            return (Drop(StitchOutcome.NoMovement, confidence: confidence), 0);
        }

        if (difference > OverlapTolerance)
        {
            return (Drop(StitchOutcome.NoMatch, "That frame didn't line up cleanly (still drawing?); dropped it.", confidence), 0);
        }

        if (best < 0)
        {
            return (Drop(StitchOutcome.ScrolledBack, "Scrolled up; scroll back down to carry on.", confidence), 0);
        }

        return (new StitchResult(StitchOutcome.Appended, best, confidence), footer);
    }

    /// <summary>
    /// The widest run of columns that changed between the frames (short gaps bridged), or null
    /// when too little changed to be scrolling: nothing at all, or only a spinner or cursor.
    /// </summary>
    private static (int Left, int Right)? ChangedColumns(byte[] a, byte[] b, int width, int height)
    {
        var changedRows = new int[width];
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            for (var x = 0; x < width; x++)
            {
                if (Math.Abs(a[row + x] - b[row + x]) > PixelTolerance)
                {
                    changedRows[x]++;
                }
            }
        }

        int bestLeft = 0, bestRight = 0, runLeft = -1, lastChanged = -1;
        for (var x = 0; x <= width; x++)
        {
            var changed = x < width && changedRows[x] >= 2;
            if (changed)
            {
                if (runLeft < 0 || x - lastChanged > MaxColumnGap)
                {
                    runLeft = x;
                }

                lastChanged = x;
            }

            if (runLeft >= 0 && (x == width || x - lastChanged > MaxColumnGap))
            {
                if (lastChanged + 1 - runLeft > bestRight - bestLeft)
                {
                    (bestLeft, bestRight) = (runLeft, lastChanged + 1);
                }

                runLeft = -1;
            }
        }

        return bestRight - bestLeft >= Math.Max(MinBand, width / 8) ? (bestLeft, bestRight) : null;
    }

    /// <summary>
    /// Rows unchanged at the same position (within the changed columns), from the top and from the
    /// bottom, each capped at 40%. "Unchanged" allows a few pixels, so a scrollbar thumb or a
    /// spinner beside a sticky header doesn't hide it.
    /// </summary>
    private static (int Header, int Footer, bool AllSame) StickyRows(byte[] a, byte[] b, int width, int height, (int Left, int Right) columns)
    {
        var limit = (int)(height * MaxStickyShare);
        var header = 0;
        while (header < height && RowUnchanged(a, b, width, header, columns))
        {
            header++;
        }

        if (header == height)
        {
            return (0, 0, true);
        }

        var footer = 0;
        while (footer < height && RowUnchanged(a, b, width, height - 1 - footer, columns))
        {
            footer++;
        }

        return (Math.Min(header, limit), Math.Min(footer, limit), false);
    }

    private static bool RowUnchanged(byte[] a, byte[] b, int width, int row, (int Left, int Right) columns)
    {
        var start = row * width;
        var changed = 0;
        for (var i = start + columns.Left; i < start + columns.Right; i++)
        {
            if (Math.Abs(a[i] - b[i]) > PixelTolerance)
            {
                changed++;
            }
        }

        return changed <= StickyRowChangeShare * (columns.Right - columns.Left);
    }

    /// <summary>
    /// The lowest (or highest) strip in the band worth matching, stepping by half a strip: enough
    /// texture, and not sitting still (a static strip would match itself at offset 0).
    /// </summary>
    private static int? TexturedStrip(byte[] previous, byte[] current, Band band, int strip, bool fromBottom)
    {
        var step = Math.Max(1, strip / 2);
        for (var top = fromBottom ? band.Bottom - strip : band.Top; top >= band.Top && top <= band.Bottom - strip; top += fromBottom ? -step : step)
        {
            if (EdgeStrength(previous, band, top, strip) >= MinEdgeStrength && !StripUnchanged(previous, current, band, top, strip))
            {
                return top;
            }
        }

        return null;
    }

    private static bool StripUnchanged(byte[] previous, byte[] current, Band band, int top, int rows)
    {
        var unchanged = 0;
        for (var y = top; y < top + rows; y++)
        {
            if (RowUnchanged(previous, current, band.Width, y, (band.Left, band.Right)))
            {
                unchanged++;
            }
        }

        return unchanged * 2 > rows;
    }

    /// <summary>
    /// Mean difference between neighboring pixels (right and below). Text and borders score high;
    /// blank space and smooth gradients, which match anywhere, score near zero.
    /// </summary>
    private static double EdgeStrength(byte[] gray, Band band, int top, int rows)
    {
        var width = band.Width;
        long sum = 0;
        for (var y = top; y < top + rows - 1; y++)
        {
            var row = y * width;
            for (var x = band.Left; x < band.Right - 1; x++)
            {
                int v = gray[row + x];
                sum += Math.Abs(v - gray[row + x + 1]) + Math.Abs(v - gray[row + width + x]);
            }
        }

        return (double)sum / ((rows - 1) * (band.Right - band.Left - 1));
    }

    /// <summary>Where the strip at <paramref name="top"/> of <paramref name="previous"/> best matches inside the band of <paramref name="current"/>.</summary>
    private static (int Top, double Confidence) FindStrip(byte[] previous, byte[] current, Band band, int top, int strip)
    {
        var width = band.Width;
        var columns = band.Right - band.Left;
        using var previousMat = Mat.FromPixelData(previous.Length / width, width, MatType.CV_8UC1, previous);
        using var currentMat = Mat.FromPixelData(current.Length / width, width, MatType.CV_8UC1, current);
        using var template = new Mat(previousMat, new Rect(band.Left, top, columns, strip));
        using var search = new Mat(currentMat, new Rect(band.Left, band.Top, columns, band.Height));
        using var scores = new Mat();
        Cv2.MatchTemplate(search, template, scores, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(scores, out _, out var best, out _, out var location);
        return (band.Top + location.Y, double.IsFinite(best) ? best : 0);
    }

    /// <summary>
    /// Mean gray difference between the previous band moved up by <paramref name="offset"/> (down
    /// when negative) and the new band, over the rows and changed columns they share.
    /// </summary>
    private static double OverlapDifference(byte[] previous, byte[] current, Band band, int offset)
    {
        var rows = band.Height - Math.Abs(offset);
        if (rows < MinBand / 2)
        {
            return double.MaxValue;
        }

        var width = band.Width;
        long sum = 0;
        for (var y = 0; y < rows; y++)
        {
            var fromPrevious = ((band.Top + Math.Max(offset, 0) + y) * width) + band.Left;
            var fromCurrent = ((band.Top + Math.Max(-offset, 0) + y) * width) + band.Left;
            for (var x = 0; x < band.Right - band.Left; x++)
            {
                sum += Math.Abs(previous[fromPrevious + x] - current[fromCurrent + x]);
            }
        }

        return (double)sum / ((long)rows * (band.Right - band.Left));
    }

    /// <summary>The rows between the sticky header and footer, and the columns that scroll.</summary>
    private readonly record struct Band(int Width, int Top, int Bottom, int Left, int Right)
    {
        public int Height => Bottom - Top;
    }

    /// <summary>BGRA to 8-bit luma (BT.601 weights in fixed point).</summary>
    private static byte[] ToGray(PixelBuffer frame)
    {
        var pixels = frame.Pixels;
        var gray = new byte[frame.Width * frame.Height];
        for (int i = 0, p = 0; i < gray.Length; i++, p += PixelBuffer.BytesPerPixel)
        {
            gray[i] = (byte)(((pixels[p] * 29) + (pixels[p + 1] * 150) + (pixels[p + 2] * 77)) >> 8);
        }

        return gray;
    }
}
