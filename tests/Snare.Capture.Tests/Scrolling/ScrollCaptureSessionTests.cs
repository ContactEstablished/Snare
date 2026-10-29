using Snare.Capture.Scrolling;

namespace Snare.Capture.Tests.Scrolling;

public class ScrollCaptureSessionTests
{
    private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(1);

    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(100);

    [Fact]
    public async Task Manual_StitchesWhileScrolling_AndStopsWhenCancelled()
    {
        var page = new SyntheticPage();
        int[] script = [0, 0, 0, 60, 60, 140, 140, 140, 260, 260];
        using var stop = new CancellationTokenSource();
        var grabs = 0;
        var session = new ScrollCaptureSession(
            () =>
            {
                var y = script[Math.Min(grabs++, script.Length - 1)];
                if (grabs >= script.Length)
                {
                    stop.Cancel(); // Matt presses Stop after the last frame
                }

                return page.Frame(y);
            },
            TimeProvider.System,
            interval: Fast,
            settleTimeout: Settle);
        var reports = new List<ScrollProgress>();

        await session.RunManualAsync(new SyncProgress(reports), stop.Token);

        ScrollStitcherTests.AssertSame(page.Expected(260), session.Compose());
        // Started plus three moves; the still frames report nothing.
        Assert.Equal([StitchOutcome.Started, StitchOutcome.Appended, StitchOutcome.Appended, StitchOutcome.Appended], reports.Select(r => r.Outcome));
        Assert.Equal(560, reports[^1].Height);
        Assert.Null(session.EndReason); // stopped, not ended by itself
    }

    [Fact]
    public async Task Manual_ReportsAWarningOnce()
    {
        var page = new SyntheticPage();
        int[] script = [0, 900, 900, 900, 100];
        using var stop = new CancellationTokenSource();
        var grabs = 0;
        var session = new ScrollCaptureSession(
            () =>
            {
                if (grabs == script.Length - 1)
                {
                    stop.Cancel();
                }

                return page.Frame(script[grabs++]);
            },
            TimeProvider.System,
            interval: Fast,
            settleTimeout: Settle);
        var reports = new List<ScrollProgress>();

        await session.RunManualAsync(new SyncProgress(reports), stop.Token);

        Assert.Single(reports, r => r.Outcome == StitchOutcome.NoMatch);
        Assert.Null(reports[^1].Warning); // the recovering frame clears it
    }

    [Fact]
    public async Task Manual_StopsByItselfAtTheCap()
    {
        var page = new SyntheticPage();
        var y = 0;
        var session = new ScrollCaptureSession(() => page.Frame(Math.Min(y += 100, 2600)), TimeProvider.System, maxHeight: 800, interval: Fast);

        await session.RunManualAsync(null, TestContext.Current.CancellationToken);

        Assert.True(session.IsFull);
        Assert.Equal(800, session.Compose().Height);
        Assert.Equal(ScrollCaptureSession.HeightLimit, session.EndReason);
    }

    [Fact]
    public async Task Preview_IsShrunkToThePanelWidth_AndThrottled()
    {
        var page = new SyntheticPage(); // 400 wide
        var y = 0;
        var session = new ScrollCaptureSession(() => page.Frame(Math.Min(y += 20, 2600)), TimeProvider.System, maxHeight: 2000, interval: Fast, previewWidth: 100);
        var reports = new List<ScrollProgress>();

        await session.RunManualAsync(new SyncProgress(reports), TestContext.Current.CancellationToken);

        var tails = reports.Where(r => r.Tail is not null).Select(r => r.Tail!).ToList();
        Assert.All(tails, t => Assert.Equal(100, t.Width));
        Assert.True(tails.Count < reports.Count, "Fast frames share a preview");
        Assert.NotNull(reports[^1].Tail); // the cap always shows the final image
    }

    [Fact]
    public async Task Automatic_ScrollsToTheEndOfThePage()
    {
        var page = new SyntheticPage(pageHeight: 1400); // the view can scroll to 1100
        var driver = new FakeDriver(view: 300, max: 1100); // half a view is 150
        var session = new ScrollCaptureSession(() => page.Frame(driver.Position), TimeProvider.System, interval: Fast, settleTimeout: Settle);

        await session.RunAutomaticAsync(driver, null, TestContext.Current.CancellationToken);

        ScrollStitcherTests.AssertSame(page.Expected(1100), session.Compose());
        Assert.Equal((1100 / 150) + 1 + ScrollCaptureSession.StepsWithoutProgress, driver.Steps); // the last steps add nothing
        Assert.Equal(ScrollCaptureSession.EndOfPage, session.EndReason);
    }

    [Fact]
    public async Task Automatic_WaitsForThePageToSettle()
    {
        var page = new SyntheticPage();
        var driver = new FakeDriver(view: 300, max: 450);
        var grabsSinceStep = 0;
        driver.Scrolled += () => grabsSinceStep = 0;
        // Each step "animates": the first grabs after it are 100, then 50 rows short.
        var session = new ScrollCaptureSession(
            () => page.Frame(driver.Steps == 0 ? 0 : driver.Position - Math.Max(0, 100 - (50 * grabsSinceStep++))),
            TimeProvider.System,
            interval: Fast,
            settleTimeout: Settle);

        await session.RunAutomaticAsync(driver, null, TestContext.Current.CancellationToken);

        ScrollStitcherTests.AssertSame(page.Expected(450), session.Compose());
    }

    [Fact]
    public async Task Automatic_WaitsForASlowRepaintBeforeSettling()
    {
        // The page shows the new position only on the third grab after a step; two identical
        // grabs of the old position must not count as "settled".
        var page = new SyntheticPage();
        var driver = new FakeDriver(view: 300, max: 450);
        var shown = 0;
        var grabsSinceStep = 0;
        driver.Scrolled += () => grabsSinceStep = 0;
        var session = new ScrollCaptureSession(
            () =>
            {
                if (++grabsSinceStep >= 3)
                {
                    shown = driver.Position;
                }

                return page.Frame(shown);
            },
            TimeProvider.System,
            interval: Fast,
            settleTimeout: Settle);

        await session.RunAutomaticAsync(driver, null, TestContext.Current.CancellationToken);

        ScrollStitcherTests.AssertSame(page.Expected(450), session.Compose());
        Assert.Equal(ScrollCaptureSession.EndOfPage, session.EndReason);
    }

    [Fact]
    public async Task Automatic_AMissedStep_IsScrolledBackAndRetriedSmaller()
    {
        var page = new SyntheticPage(pageHeight: 1400);
        var driver = new FakeDriver(view: 300, max: 1100) { Overshoot = 1 }; // the second step jumps 600 px
        var session = new ScrollCaptureSession(() => page.Frame(driver.Position), TimeProvider.System, interval: Fast, settleTimeout: Settle);

        await session.RunAutomaticAsync(driver, null, TestContext.Current.CancellationToken);

        ScrollStitcherTests.AssertSame(page.Expected(1100), session.Compose());
        Assert.Contains(-0.5, driver.Fractions);  // went back
        Assert.Contains(0.25, driver.Fractions);  // then a smaller step
        Assert.Equal(ScrollCaptureSession.EndOfPage, session.EndReason);
    }

    [Fact]
    public async Task Automatic_GivesUpAfterRepeatedMisses_AndSaysWhy()
    {
        var page = new SyntheticPage();
        var driver = new FakeDriver(view: 300, max: 2600) { Overshoot = int.MaxValue }; // every step jumps too far
        var session = new ScrollCaptureSession(() => page.Frame(driver.Position), TimeProvider.System, interval: Fast, settleTimeout: Settle);

        await session.RunAutomaticAsync(driver, null, TestContext.Current.CancellationToken);

        Assert.Equal(page.Viewport + 150, session.Compose().Height); // the first step was fine
        Assert.Equal(ScrollCaptureSession.LostTrack, session.EndReason);
    }

    [Fact]
    public async Task Automatic_StopsWhenTheDriverCantScroll()
    {
        var page = new SyntheticPage();
        var session = new ScrollCaptureSession(() => page.Frame(0), TimeProvider.System, interval: Fast, settleTimeout: Settle);

        await session.RunAutomaticAsync(new FakeDriver(300, 0) { CanScroll = false }, null, TestContext.Current.CancellationToken);

        Assert.Equal(page.Viewport, session.Compose().Height);
        Assert.Equal(ScrollCaptureSession.CantScroll, session.EndReason);
    }

    /// <summary>
    /// Moves a position by a share of the view. With <see cref="Overshoot"/>, that many downward
    /// steps (after the first) jump 600 px instead; an upward step undoes the last move exactly.
    /// </summary>
    private sealed class FakeDriver(int view, int max) : IScrollDriver
    {
        private int _downSteps;
        private int _lastMove;

        public event Action? Scrolled;

        public int Position { get; private set; }

        public int Steps { get; private set; }

        public List<double> Fractions { get; } = [];

        public bool CanScroll { get; init; } = true;

        public int Overshoot { get; init; }

        public Task<bool> ScrollAsync(double viewFraction, CancellationToken cancellationToken)
        {
            Steps++;
            Fractions.Add(viewFraction);
            var move = (int)(viewFraction * view);
            if (viewFraction > 0 && ++_downSteps > 1 && _downSteps - 1 <= Overshoot)
            {
                move = 600;
            }
            else if (viewFraction < 0)
            {
                move = -_lastMove;
            }

            var before = Position;
            Position = Math.Clamp(Position + move, 0, max);
            _lastMove = Position - before;
            Scrolled?.Invoke();
            return Task.FromResult(CanScroll);
        }
    }

    /// <summary>Progress that records on the calling thread (Progress&lt;T&gt; posts asynchronously).</summary>
    private sealed class SyncProgress(List<ScrollProgress> reports) : IProgress<ScrollProgress>
    {
        public void Report(ScrollProgress value) => reports.Add(value);
    }
}
