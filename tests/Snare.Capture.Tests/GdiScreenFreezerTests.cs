namespace Snare.Capture.Tests;

/// <summary>
/// Grabs the real desktop, so it only runs when asked for explicitly on a machine with a
/// signed-in desktop: <c>tests\Snare.Capture.Tests\bin\x64\Debug\net10.0-windows10.0.22621.0\Snare.Capture.Tests.exe -explicit only -diagnostics</c>.
/// The test exe has the app's Per-Monitor V2 manifest, so it sees physical pixels as Snare does.
/// </summary>
public class GdiScreenFreezerTests
{
    [Fact(Explicit = true)]
    public void Freeze_GrabsEveryMonitorAtItsPhysicalSize()
    {
        var desktop = new GdiScreenFreezer(TimeProvider.System).Freeze(includeCursor: true);

        Assert.NotEmpty(desktop.Monitors);
        var primary = Assert.Single(desktop.Monitors, m => m.Monitor.IsPrimary);
        Assert.Equal((0, 0), (primary.Monitor.Bounds.X, primary.Monitor.Bounds.Y));

        foreach (var monitor in desktop.Monitors)
        {
            TestContext.Current.SendDiagnosticMessage(
                $"{monitor.Monitor.DeviceName} {monitor.Monitor.Bounds} dpi {monitor.Monitor.Dpi} primary {monitor.Monitor.IsPrimary}");
            Assert.Equal(monitor.Monitor.Bounds.Width, monitor.Frame.Width);
            Assert.Equal(monitor.Monitor.Bounds.Height, monitor.Frame.Height);

            // GDI leaves alpha undefined; the grabber must make every pixel opaque.
            var pixels = monitor.Frame.Pixels;
            for (var i = 3; i < pixels.Length; i += 4)
            {
                Assert.True(pixels[i] == 0xFF, $"Pixel {i / 4} on {monitor.Monitor.DeviceName} is not opaque.");
            }
        }

        Assert.NotEmpty(desktop.Windows);
        Assert.DoesNotContain(desktop.Windows, w => w.Bounds.IsEmpty);
        foreach (var window in desktop.Windows.Take(5))
        {
            TestContext.Current.SendDiagnosticMessage($"window {window.ProcessName} \"{window.Title}\" {window.Bounds}");
        }

        TestContext.Current.SendDiagnosticMessage($"source under cursor: {desktop.SourceUnderCursor()}");

        var composed = VirtualDesktopComposer.Compose(desktop);
        Assert.Equal(desktop.Bounds.Width, composed.Width);
        Assert.Equal(desktop.Bounds.Height, composed.Height);
    }
}
