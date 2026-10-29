using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Snare.Core.Captures;

namespace Snare.App.Spikes;

/// <summary>Phase 0 spike: freeze every monitor and show the frozen frames in per-monitor overlays.</summary>
public sealed class FreezeOverlaySpike(IScreenFreezer freezer, ILogger<FreezeOverlaySpike> logger)
{
    private readonly List<Window> _open = [];

    /// <summary>Raised by Ctrl+S in an overlay, to open the Skia canvas spike on the frozen desktop.</summary>
    public event EventHandler<FrozenDesktop>? CanvasRequested;

    public void Run()
    {
        if (_open.Count > 0)
        {
            return;
        }

        var started = TimeProvider.System.GetTimestamp();
        var desktop = freezer.Freeze();
        logger.LogInformation(
            "Froze {Count} monitors in {Elapsed} ms; desktop bounds {Bounds}",
            desktop.Monitors.Count, TimeProvider.System.GetElapsedTime(started).TotalMilliseconds, desktop.Bounds);

        foreach (var monitor in desktop.Monitors)
        {
            logger.LogInformation("Monitor {Device} bounds {Bounds} dpi {Dpi} primary {Primary}",
                monitor.Monitor.DeviceName, monitor.Monitor.Bounds, monitor.Monitor.Dpi, monitor.Monitor.IsPrimary);

            var window = new FreezeOverlayWindow(desktop, monitor);
            window.KeyDown += (_, e) => OnKeyDown(e, desktop);
            _open.Add(window);
            window.Show();
        }

        _open[0].Activate();
    }

    private void OnKeyDown(KeyEventArgs e, FrozenDesktop desktop)
    {
        if (e.Key == Key.Escape)
        {
            CloseAll();
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CloseAll();
            CanvasRequested?.Invoke(this, desktop);
        }
    }

    private void CloseAll()
    {
        foreach (var window in _open)
        {
            window.Close();
        }

        _open.Clear();
    }
}
