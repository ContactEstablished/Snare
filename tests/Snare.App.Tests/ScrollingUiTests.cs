using Snare.App.Scrolling;
using Snare.Capture.Scrolling;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Imaging;

namespace Snare.App.Tests;

public class ScrollingUiTests
{
    private static ScrollPanelViewModel Panel(List<string> calls) => new(
        () => { calls.Add("manual"); return Task.CompletedTask; },
        () => { calls.Add("auto"); return Task.CompletedTask; },
        () => { calls.Add("stop"); return Task.CompletedTask; },
        () => { calls.Add("cancel"); return Task.CompletedTask; });

    [Fact]
    public async Task Panel_OnlyOffersWhatFitsTheState()
    {
        var calls = new List<string>();
        var panel = Panel(calls);

        Assert.True(panel.StartManualCommand.CanExecute(null));
        Assert.True(panel.StartAutomaticCommand.CanExecute(null));
        Assert.False(panel.StopCommand.CanExecute(null));
        await panel.StartManualCommand.ExecuteAsync(null);

        panel.SetState(ScrollPanelState.Manual);
        Assert.False(panel.StartManualCommand.CanExecute(null));
        Assert.False(panel.StartAutomaticCommand.CanExecute(null));
        Assert.True(panel.StopCommand.CanExecute(null));
        Assert.Contains("Ctrl+Shift+PrtSc", panel.HintText, StringComparison.Ordinal);
        await panel.StopCommand.ExecuteAsync(null);

        panel.SetState(ScrollPanelState.Finishing);
        Assert.False(panel.StopCommand.CanExecute(null));
        Assert.False(panel.CancelCommand.CanExecute(null));
        Assert.Equal(["manual", "stop"], calls);
    }

    [Fact]
    public void Panel_ShowsHeightWarningAndPreview()
    {
        var panel = Panel([]);
        var tail = PixelBuffer.Filled(10, 20, 0xFFFFFFFF);

        panel.Apply(new ScrollProgress(12_345, 30_000, StitchOutcome.Appended, null, tail));
        panel.Apply(new ScrollProgress(12_345, 30_000, StitchOutcome.NoMatch, "Couldn't line up that frame; scroll a little slower.", null));

        Assert.Equal(string.Create(System.Globalization.CultureInfo.CurrentCulture, $"{12_345:N0} px of {30_000:N0}"), panel.HeightText);
        Assert.StartsWith("Couldn't line up", panel.Warning, StringComparison.Ordinal);
        Assert.Same(tail, panel.Preview); // a report without a tail keeps the last one

        panel.Apply(new ScrollProgress(30_000, 30_000, StitchOutcome.LimitReached, null, tail));
        Assert.Equal("Reached the height limit.", panel.Warning);
    }

    [Fact]
    public void Windows_LoadTheirXaml()
    {
        Sta.Run(() =>
        {
            var bounds = new PixelRect(0, 0, 1920, 1080);
            var monitor = new MonitorInfo("m", bounds, bounds, 96, true);
            var panel = Panel([]);
            var window = new ScrollPanelWindow(panel, new PixelRect(100, 100, 800, 600), monitor);
            Assert.Same(panel, window.DataContext);

            panel.Apply(new ScrollProgress(600, 30_000, StitchOutcome.Started, null, PixelBuffer.Filled(80, 60, 0xFF336699)));
            Assert.NotNull(window.PreviewImage.Source);

            Assert.NotNull(new ScrollAreaBorder(new PixelRect(100, 100, 800, 600), monitor).Content);
        });
    }
}
