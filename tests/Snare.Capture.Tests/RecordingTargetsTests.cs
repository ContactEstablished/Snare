using Snare.Capture.Selection;
using Snare.Core.Geometry;

namespace Snare.Capture.Tests;

public class RecordingTargetsTests
{
    [Fact]
    public void Region_IsClippedToTheMonitorUnderItsCenter()
    {
        var desktop = TestDesktops.Stacked();

        // Mostly on the primary (0,0,100,40), poking into the upper monitor.
        var target = RecordingTargets.From(new SelectionResult(new PixelRect(30, -5, 40, 30), CaptureTarget.Region), desktop);

        Assert.NotNull(target);
        Assert.Equal("primary", target.DisplayDeviceName);
        Assert.Equal(new PixelRect(30, 0, 40, 25), target.Area);
        Assert.Null(target.WindowHandle);
    }

    [Fact]
    public void Window_KeepsItsHandle()
    {
        var target = RecordingTargets.From(
            new SelectionResult(TestDesktops.Editor.Bounds, CaptureTarget.Window, TestDesktops.Editor), TestDesktops.Stacked());

        Assert.Equal(TestDesktops.Editor.Handle, target!.WindowHandle);
    }

    [Fact]
    public void UpperMonitor_IsAWholeDisplayTarget()
    {
        var target = RecordingTargets.From(new SelectionResult(new PixelRect(20, -40, 60, 40), CaptureTarget.Monitor), TestDesktops.Stacked());

        Assert.True(target!.IsWholeDisplay);
        Assert.Equal(new PixelRect(0, 0, 60, 40), target.AreaOnDisplay);
    }

    [Fact]
    public void AllMonitors_CantBeRecorded()
    {
        Assert.Null(RecordingTargets.From(new SelectionResult(new PixelRect(0, -40, 100, 80), CaptureTarget.AllMonitors), TestDesktops.Stacked()));
    }

    [Fact]
    public void CenterInTheGap_UsesTheMonitorItOverlapsMost()
    {
        // Center (10, -10) is left of the upper monitor; the rect overlaps the primary most.
        var target = RecordingTargets.From(new SelectionResult(new PixelRect(0, -30, 20, 40), CaptureTarget.Region), TestDesktops.Stacked());

        Assert.Equal("primary", target!.DisplayDeviceName);
        Assert.Equal(new PixelRect(0, 0, 20, 10), target.Area);
    }
}
