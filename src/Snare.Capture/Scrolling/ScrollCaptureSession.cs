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

    /// <summary>How long automatic mode waits for a page to stop moving after a step.</summary>
    public static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(1.5);

    /// <summary>Automatic mode stops after this many steps in a row that add nothing.</summary>
    public const int StepsWithoutProgress = 2;

    private readonly Func<PixelBuffer> _grab;
    private readonly TimeProvider _time;
    private readonly TimeSpan _interval;
    private readonly int _tailRows;
    private readonly ScrollStitcher _stitcher;

    public ScrollCaptureSession(
        Func<PixelBuffer> grab, TimeProvider time, int maxHeight = ScrollStitcher.DefaultMaxHeight, TimeSpan? interval = null, int tailRows = 1200)
    {
        _grab = grab ?? throw new ArgumentNullException(nameof(grab));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _interval = interval ?? DefaultInterval;
        _tailRows = tailRows;
        _stitcher = new ScrollStitcher(maxHeight);
    }

    public bool HasImage => _stitcher.Height > 0;

    public bool IsFull => _stitcher.IsFull;

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
    /// Automatic mode: a settled first frame, then scroll, wait for the page to settle, stitch;
    /// until two steps in a row add nothing (the end of the page), the height cap, the driver
    /// can't scroll, or cancellation.
    /// </summary>
    public async Task RunAutomaticAsync(IScrollDriver driver, IProgress<ScrollProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(driver);
        string? lastWarning = null;
        try
        {
            Report(progress, _stitcher.Add(await SettledFrameAsync(cancellationToken)), ref lastWarning);
            var idle = 0;
            while (!_stitcher.IsFull && idle < StepsWithoutProgress)
            {
                if (!await driver.ScrollAsync(cancellationToken))
                {
                    return;
                }

                var result = _stitcher.Add(await SettledFrameAsync(cancellationToken));
                Report(progress, result, ref lastWarning);
                idle = result.Outcome is StitchOutcome.Appended or StitchOutcome.LimitReached ? 0 : idle + 1;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public PixelBuffer Compose() => _stitcher.Compose();

    /// <summary>Grabs until two grabs in a row are identical (rendering and smooth scrolling are done), or the timeout.</summary>
    private async Task<PixelBuffer> SettledFrameAsync(CancellationToken cancellationToken)
    {
        var deadline = _time.GetTimestamp() + (long)(SettleTimeout.TotalSeconds * _time.TimestampFrequency);
        var previous = _grab();
        while (_time.GetTimestamp() < deadline)
        {
            await Task.Delay(_interval, _time, cancellationToken);
            var current = _grab();
            if (current.Pixels.AsSpan().SequenceEqual(previous.Pixels))
            {
                return current;
            }

            previous = current;
        }

        return previous;
    }

    /// <summary>Reports frames that changed something, and warnings once each; a still page reports nothing.</summary>
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
        progress.Report(new ScrollProgress(
            _stitcher.Height, _stitcher.MaxHeight, result.Outcome, result.Warning, changed ? _stitcher.Tail(_tailRows) : null));
    }
}
