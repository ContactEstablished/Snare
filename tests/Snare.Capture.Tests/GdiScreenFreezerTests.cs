using Snare.Core.Geometry;

namespace Snare.Capture.Tests;

/// <summary>
/// Grabs the real desktop, so it only runs when asked for explicitly
/// (<c>dotnet test -- --explicit only</c>) on a machine with a signed-in desktop.
/// </summary>
public class GdiScreenFreezerTests
{
    [Fact(Explicit = true)]
    public void Freeze_GrabsEveryMonitorAtItsPhysicalSize()
    {
        var desktop = new GdiScreenFreezer(TimeProvider.System).Freeze();

        Assert.NotEmpty(desktop.Monitors);
        Assert.Single(desktop.Monitors, m => m.Monitor.IsPrimary);
        foreach (var monitor in desktop.Monitors)
        {
            Assert.Equal(monitor.Monitor.Bounds.Width, monitor.Frame.Width);
            Assert.Equal(monitor.Monitor.Bounds.Height, monitor.Frame.Height);
            TestContext.Current.SendDiagnosticMessage(
                $"{monitor.Monitor.DeviceName} {monitor.Monitor.Bounds} dpi {monitor.Monitor.Dpi} primary {monitor.Monitor.IsPrimary}");
        }

        var composed = VirtualDesktopComposer.Compose(desktop);
        Assert.Equal(desktop.Bounds.Width, composed.Width);
        Assert.Equal(desktop.Bounds.Height, composed.Height);
        Assert.Equal(new PixelPoint(0, 0), new PixelPoint(
            desktop.Monitors.Single(m => m.Monitor.IsPrimary).Monitor.Bounds.X,
            desktop.Monitors.Single(m => m.Monitor.IsPrimary).Monitor.Bounds.Y));
    }
}
