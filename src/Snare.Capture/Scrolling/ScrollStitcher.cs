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

    /// <summary>A row is "identical" when its mean gray difference is at most this.</summary>
    private const double RowTolerance = 1.0;

    /// <summary>The largest mean gray difference allowed over the whole overlap of a match.</summary>
    private const double OverlapTolerance = 6.0;

    /// <summary>A template strip needs at least this gray standard deviation to be worth matching.</summary>
    private const double MinTextureStdDev = 4.0;

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

        var (header, footer, allSame) = StickyRows(previous, current, width, height);
        if (allSame)
        {
            return (Drop(StitchOutcome.NoMovement), 0);
        }

        var bandTop = header;
        var bandBottom = height - footer;
        if (bandBottom - bandTop < MinBand)
        {
            return (Drop(StitchOutcome.NoMatch, "Too little of the area scrolls to line up frames."), 0);
        }

        // Small, so a blank stretch at the bottom of the view costs little range; the overlap check
        // below catches the odd false match a small strip allows.
        var strip = Math.Clamp((bandBottom - bandTop) / 8, 16, 48);
        var templateTop = TexturedStrip(previous, width, bandTop, bandBottom, strip, fromBottom: true);
        if (templateTop is not { } top)
        {
            return (Drop(StitchOutcome.NoMatch, "Nothing to line up in this part of the page; keep scrolling."), 0);
        }

        var (foundTop, confidence) = FindStrip(previous, current, width, top, strip, bandTop, bandBottom);
        if (confidence < MinConfidence
            && TexturedStrip(previous, width, bandTop, bandBottom, strip, fromBottom: false) is { } upper)
        {
            // The bottom strip leaves the view when the page scrolls up; one from the top doesn't.
            var (upperFound, upperConfidence) = FindStrip(previous, current, width, upper, strip, bandTop, bandBottom);
            if (upperConfidence >= MinConfidence && upperFound > upper)
            {
                return (Drop(StitchOutcome.ScrolledBack, "Scrolled up; scroll back down to carry on.", upperConfidence), 0);
            }
        }

        if (confidence < MinConfidence)
        {
            return (Drop(StitchOutcome.NoMatch, "Couldn't line up that frame; scroll a little slower.", confidence), 0);
        }

        var offset = top - foundTop;
        if (offset == 0)
        {
            return (Drop(StitchOutcome.NoMovement, confidence: confidence), 0);
        }

        if (offset < 0)
        {
            return (Drop(StitchOutcome.ScrolledBack, "Scrolled up; scroll back down to carry on.", confidence), 0);
        }

        if (OverlapDifference(previous, current, width, bandTop, bandBottom, offset) > OverlapTolerance)
        {
            return (Drop(StitchOutcome.NoMatch, "That frame didn't line up cleanly (still drawing?); dropped it.", confidence), 0);
        }

        return (new StitchResult(StitchOutcome.Appended, offset, confidence), footer);
    }

    /// <summary>Rows identical at the same position, from the top and from the bottom, each capped at 40%.</summary>
    private static (int Header, int Footer, bool AllSame) StickyRows(byte[] a, byte[] b, int width, int height)
    {
        var limit = (int)(height * MaxStickyShare);
        var header = 0;
        while (header < height && RowsMatch(a, b, width, header))
        {
            header++;
        }

        if (header == height)
        {
            return (0, 0, true);
        }

        var footer = 0;
        while (footer < height && RowsMatch(a, b, width, height - 1 - footer))
        {
            footer++;
        }

        return (Math.Min(header, limit), Math.Min(footer, limit), false);
    }

    private static bool RowsMatch(byte[] a, byte[] b, int width, int row)
    {
        var start = row * width;
        long sum = 0;
        for (var i = start; i < start + width; i++)
        {
            sum += Math.Abs(a[i] - b[i]);
        }

        return sum <= RowTolerance * width;
    }

    /// <summary>The lowest (or highest) strip in the band with enough texture to match, stepping by half a strip.</summary>
    private static int? TexturedStrip(byte[] gray, int width, int bandTop, int bandBottom, int strip, bool fromBottom)
    {
        var step = Math.Max(1, strip / 2);
        for (var top = fromBottom ? bandBottom - strip : bandTop; top >= bandTop && top <= bandBottom - strip; top += fromBottom ? -step : step)
        {
            if (StdDev(gray, width, top, strip) >= MinTextureStdDev)
            {
                return top;
            }
        }

        return null;
    }

    private static double StdDev(byte[] gray, int width, int top, int rows)
    {
        long sum = 0;
        long squares = 0;
        var start = top * width;
        var count = rows * width;
        for (var i = start; i < start + count; i++)
        {
            int v = gray[i];
            sum += v;
            squares += v * v;
        }

        var mean = (double)sum / count;
        return Math.Sqrt(Math.Max(0, ((double)squares / count) - (mean * mean)));
    }

    /// <summary>Where the strip at <paramref name="top"/> of <paramref name="previous"/> best matches inside the band of <paramref name="current"/>.</summary>
    private static (int Top, double Confidence) FindStrip(
        byte[] previous, byte[] current, int width, int top, int strip, int bandTop, int bandBottom)
    {
        using var previousMat = Mat.FromPixelData(previous.Length / width, width, MatType.CV_8UC1, previous);
        using var currentMat = Mat.FromPixelData(current.Length / width, width, MatType.CV_8UC1, current);
        using var template = new Mat(previousMat, new Rect(0, top, width, strip));
        using var band = new Mat(currentMat, new Rect(0, bandTop, width, bandBottom - bandTop));
        using var scores = new Mat();
        Cv2.MatchTemplate(band, template, scores, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(scores, out _, out var best, out _, out var location);
        return (bandTop + location.Y, double.IsFinite(best) ? best : 0);
    }

    /// <summary>Mean gray difference between the previous band moved up by <paramref name="offset"/> and the new band.</summary>
    private static double OverlapDifference(byte[] previous, byte[] current, int width, int bandTop, int bandBottom, int offset)
    {
        var rows = bandBottom - bandTop - offset;
        if (rows <= 0)
        {
            return double.MaxValue;
        }

        long sum = 0;
        var fromPrevious = (bandTop + offset) * width;
        var fromCurrent = bandTop * width;
        var count = rows * width;
        for (var i = 0; i < count; i++)
        {
            sum += Math.Abs(previous[fromPrevious + i] - current[fromCurrent + i]);
        }

        return (double)sum / count;
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
