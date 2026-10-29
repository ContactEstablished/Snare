using Snare.Capture.Scrolling;

namespace Snare.Capture.Tests.Scrolling;

public class ScrollCaptureSessionTests
{
    private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(1);

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
            interval: Fast);
        var reports = new List<ScrollProgress>();

        await session.RunManualAsync(new SyncProgress(reports), stop.Token);

        ScrollStitcherTests.AssertSame(page.Expected(260), session.Compose());
        // Started plus three moves; the still frames report nothing.
        Assert.Equal([StitchOutcome.Started, StitchOutcome.Appended, StitchOutcome.Appended, StitchOutcome.Appended], reports.Select(r => r.Outcome));
        Assert.Equal(560, reports[^1].Height);
        Assert.NotNull(reports[^1].Tail);
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
            interval: Fast);
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
    }

    [Fact]
    public async Task Automatic_ScrollsToTheEndOfThePage()
    {
        var page = new SyntheticPage(pageHeight: 1400); // the view can scroll to 1100
        var driver = new FakeDriver(step: 150, max: 1100);
        var session = new ScrollCaptureSession(() => page.Frame(driver.Position), TimeProvider.System, interval: Fast);

        await session.RunAutomaticAsync(driver, null, TestContext.Current.CancellationToken);

        ScrollStitcherTests.AssertSame(page.Expected(1100), session.Compose());
        Assert.Equal(1100 / 150 + 1 + ScrollCaptureSession.StepsWithoutProgress, driver.Steps); // the last steps add nothing
    }

    [Fact]
    public async Task Automatic_WaitsForThePageToSettle()
    {
        var page = new SyntheticPage();
        var driver = new FakeDriver(step: 150, max: 450);
        var grabsSinceStep = 0;
        driver.Scrolled += () => grabsSinceStep = 0;
        // Each step "animates": the first grabs after it are 100, then 50 rows short.
        var session = new ScrollCaptureSession(
            () => page.Frame(driver.Steps == 0 ? 0 : driver.Position - Math.Max(0, 100 - (50 * grabsSinceStep++))),
            TimeProvider.System,
            interval: Fast);

        await session.RunAutomaticAsync(driver, null, TestContext.Current.CancellationToken);

        ScrollStitcherTests.AssertSame(page.Expected(450), session.Compose());
    }

    [Fact]
    public async Task Automatic_StopsWhenTheDriverCantScroll()
    {
        var page = new SyntheticPage();
        var session = new ScrollCaptureSession(() => page.Frame(0), TimeProvider.System, interval: Fast);

        await session.RunAutomaticAsync(new FakeDriver(0, 0) { CanScroll = false }, null, TestContext.Current.CancellationToken);

        Assert.Equal(page.Viewport, session.Compose().Height);
    }

    private sealed class FakeDriver(int step, int max) : IScrollDriver
    {
        public event Action? Scrolled;

        public int Position { get; private set; }

        public int Steps { get; private set; }

        public bool CanScroll { get; init; } = true;

        public Task<bool> ScrollAsync(CancellationToken cancellationToken)
        {
            Steps++;
            Position = Math.Min(max, Position + step);
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
