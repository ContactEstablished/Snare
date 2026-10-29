using Snare.Core.Imaging;

namespace Snare.Capture.Scrolling;

/// <summary>
/// Grabs a fixed area repeatedly and feeds a <see cref="ScrollStitcher"/>: on a timer while Matt
/// scrolls (manual), or one settled frame per scroll step (automatic). Run it off the UI thread;
/// cancelling the token ends either loop normally, and <see cref="Compose"/> returns the image.
/// </summary>
public sealed class ScrollCaptureSession
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>How long automatic mode waits for a step to show, and then for the page to stop moving.</summary>
    public static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(1.5);

    /// <summary>The live preview gets a new image at most this often.</summary>
    public static readonly TimeSpan PreviewInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Automatic mode stops after this many steps in a row that don't move the page.</summary>
    public const int StepsWithoutProgress = 2;

    /// <summary>Automatic mode gives up after this many steps in a row it can't line up.</summary>
    public const int MissesBeforeGivingUp = 4;

    /// <summary>A step is this share of the view; after a miss it halves, down to <see cref="SmallestStep"/>.</summary>
    public const double DefaultStep = 0.5;

    public const double SmallestStep = 0.125;

    public const string EndOfPage = "Reached the end of the page.";

    public const string HeightLimit = "Reached the height limit.";

    public const string LostTrack = "Couldn't line up the page, so it stopped early. Try manual mode.";

    public const string CantScroll = "Couldn't scroll this window.";

    private readonly Func<PixelBuffer> _grab;
    private readonly TimeProvider _time;
    private readonly TimeSpan _interval;
    private readonly int _tailRows;
    private readonly int _previewWidth;
    private readonly TimeSpan _settleTimeout;
    private readonly ScrollStitcher _stitcher;
    private long _lastPreview = long.MinValue;

    public ScrollCaptureSession(
        Func<PixelBuffer> grab,
        TimeProvider time,
        int maxHeight = ScrollStitcher.DefaultMaxHeight,
        TimeSpan? interval = null,
        int tailRows = 1200,
        int previewWidth = 480,
        TimeSpan? settleTimeout = null)
    {
        _grab = grab ?? throw new ArgumentNullException(nameof(grab));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _interval = interval ?? DefaultInterval;
        _tailRows = tailRows;
        _previewWidth = previewWidth;
        _settleTimeout = settleTimeout ?? SettleTimeout;
        _stitcher = new ScrollStitcher(maxHeight);
    }

    public bool HasImage => _stitcher.Height > 0;

    public bool IsFull => _stitcher.IsFull;

    /// <summary>Why the capture ended by itself (end of page, height limit, lost track), or null when it was stopped.</summary>
    public string? EndReason { get; private set; }

    /// <summary>Manual mode: grabs every interval until cancelled or the height cap.</summary>
    public async Task RunManualAsync(IProgress<ScrollProgress>? progress, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval, _time);
        string? lastWarning = null;
        try
        {
            do
            {
                var result = _stitcher.Add(_grab());
                Report(progress, result, ref lastWarning);
                if (_stitcher.IsFull)
                {
                    EndReason = HeightLimit;
                    return;
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// Automatic mode: a settled first frame, then scroll, wait for the page to settle, stitch.
    /// A step that can't be lined up is scrolled back and retried at half the size. Ends at the
    /// end of the page (two steps that don't move it), the height cap, too many misses in a row,
    /// a driver that can't scroll, or cancellation.
    /// </summary>
    public async Task RunAutomaticAsync(IScrollDriver driver, IProgress<ScrollProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(driver);
        string? lastWarning = null;
        try
        {
            var reference = await SettledFrameAsync(null, cancellationToken);
            Report(progress, _stitcher.Add(reference), ref lastWarning);
            var step = DefaultStep;
            var idle = 0;
            var misses = 0;
            while (!_stitcher.IsFull)
            {
                if (!await driver.ScrollAsync(step, cancellationToken))
                {
                    EndReason = CantScroll;
                    return;
                }

                var frame = await SettledFrameAsync(reference, cancellationToken);
                var result = _stitcher.Add(frame);
                Report(progress, result, ref lastWarning);
                switch (result.Outcome)
                {
                    case StitchOutcome.Appended or StitchOutcome.LimitReached:
                        (reference, idle, misses) = (frame, 0, 0);
                        step = Math.Min(DefaultStep, step * 2);
                        break;
                    case StitchOutcome.NoMovement:
                        if (++idle >= StepsWithoutProgress)
                        {
                            EndReason = EndOfPage;
                            return;
                        }

                        break;
                    default:
                        // Couldn't line it up: go back to the last good frame and take a smaller step.
                        if (++misses >= MissesBeforeGivingUp)
                        {
                            EndReason = LostTrack;
                            return;
                        }

                        await driver.ScrollAsync(-step, cancellationToken);
                        await SettledFrameAsync(frame, cancellationToken);
                        step = Math.Max(SmallestStep, step / 2);
                        break;
                }
            }

            EndReason = HeightLimit;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public PixelBuffer Compose() => _stitcher.Compose();

    /// <summary>
    /// After a step: waits until the grab differs from <paramref name="before"/> (the page has
    /// started to repaint), then until two grabs in a row are identical (it has stopped moving).
    /// Each wait gives up after the settle timeout (<see cref="SettleTimeout"/> by default); a page that never changed is returned as is.
    /// </summary>
    private async Task<PixelBuffer> SettledFrameAsync(PixelBuffer? before, CancellationToken cancellationToken)
    {
        var frame = _grab();
        if (before is not null)
        {
            var changeDeadline = Deadline();
            while (Same(frame, before) && _time.GetTimestamp() < changeDeadline)
            {
                await Task.Delay(_interval, _time, cancellationToken);
                frame = _grab();
            }

            if (Same(frame, before))
            {
                return frame;
            }
        }

        var settleDeadline = Deadline();
        while (_time.GetTimestamp() < settleDeadline)
        {
            await Task.Delay(_interval, _time, cancellationToken);
            var next = _grab();
            if (Same(next, frame))
            {
                return next;
            }

            frame = next;
        }

        return frame;
    }

    private long Deadline() => _time.GetTimestamp() + (long)(_settleTimeout.TotalSeconds * _time.TimestampFrequency);

    private static bool Same(PixelBuffer a, PixelBuffer b) => a.Pixels.AsSpan().SequenceEqual(b.Pixels);

    /// <summary>
    /// Reports frames that changed something, and warnings once each; a still page reports
    /// nothing. The preview image is shrunk to the panel's width and sent a few times a second.
    /// </summary>
    private void Report(IProgress<ScrollProgress>? progress, StitchResult result, ref string? lastWarning)
    {
        if (progress is null)
        {
            return;
        }

        var changed = result.Outcome is StitchOutcome.Started or StitchOutcome.Appended or StitchOutcome.LimitReached;
        if (!changed && result.Warning == lastWarning)
        {
            return;
        }

        lastWarning = result.Warning;
        PixelBuffer? tail = null;
        var now = _time.GetTimestamp();
        if (changed && (result.Outcome != StitchOutcome.Appended || _time.GetElapsedTime(_lastPreview, now) >= PreviewInterval || _lastPreview == long.MinValue))
        {
            tail = Shrink(_stitcher.Tail(_tailRows), _previewWidth);
            _lastPreview = now;
        }

        progress.Report(new ScrollProgress(_stitcher.Height, _stitcher.MaxHeight, result.Outcome, result.Warning, tail));
    }

    /// <summary>Box-averages the image down by a whole factor so it's at most <paramref name="maxWidth"/> wide.</summary>
    internal static PixelBuffer Shrink(PixelBuffer image, int maxWidth)
    {
        var factor = (image.Width + maxWidth - 1) / maxWidth;
        if (factor <= 1)
        {
            return image;
        }

        var width = image.Width / factor;
        var height = Math.Max(1, image.Height / factor);
        var pixels = new byte[PixelBuffer.ByteCount(width, height)];
        var source = image.Pixels;
        var area = factor * factor;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                int b = 0, g = 0, r = 0;
                for (var dy = 0; dy < factor && (y * factor) + dy < image.Height; dy++)
                {
                    var i = ((((y * factor) + dy) * image.Width) + (x * factor)) * 4;
                    for (var dx = 0; dx < factor; dx++, i += 4)
                    {
                        b += source[i];
                        g += source[i + 1];
                        r += source[i + 2];
                    }
                }

                var o = ((y * width) + x) * 4;
                pixels[o] = (byte)(b / area);
                pixels[o + 1] = (byte)(g / area);
                pixels[o + 2] = (byte)(r / area);
                pixels[o + 3] = 255;
            }
        }

        return new PixelBuffer(width, height, pixels);
    }
}
