using Microsoft.Extensions.Logging;
using Snare.App.Capture;
using Snare.App.Services;
using Snare.Capture.Scrolling;
using Snare.Capture.Selection;
using Snare.Core.Captures;
using Snare.Core.Geometry;

namespace Snare.App.Scrolling;

/// <summary>
/// Ctrl+Shift+PrtSc: pick an area on a frozen screen, then grab it while Matt scrolls (or while
/// Snare scrolls it), and hand the stitched image to the normal capture path. Runs on the UI
/// thread; grabbing and stitching run on the thread pool.
/// </summary>
public sealed class ScrollCaptureService(
    IScreenFreezer freezer,
    CaptureOverlay overlay,
    CaptureService capture,
    TrayIconService tray,
    TimeProvider time,
    ILogger<ScrollCaptureService> logger)
{
    private readonly CaptureService _capture = capture;
    private readonly TrayIconService _tray = tray;
    private readonly TimeProvider _time = time;
    private readonly ILogger _logger = logger;
    private Run? _run;
    private bool _choosing;

    public bool IsActive => _run is not null || _choosing;

    /// <summary>The hotkey and tray entry: starts a capture, or stops (and saves) the one running.</summary>
    public async Task ToggleAsync()
    {
        if (_run is { } run)
        {
            await run.FinishAsync(save: run.Panel.State != ScrollPanelState.Ready);
            return;
        }

        if (_choosing || overlay.IsOpen)
        {
            return;
        }

        _choosing = true;
        try
        {
            var desktop = freezer.Freeze(includeCursor: false);
            var selection = await overlay.SelectAsync(desktop);
            if (selection is null)
            {
                return;
            }

            if (RecordingTargets.From(selection, desktop) is not { } target)
            {
                _tray.Notify("Scrolling capture", "A scrolling capture covers one monitor. Pick a region, a window, or one monitor (F).");
                return;
            }

            var monitor = desktop.Monitors.First(m => m.Monitor.DeviceName == target.DisplayDeviceName).Monitor;
            var source = selection.Window is { } w ? new CaptureSource(w.ProcessName, w.Title) : desktop.SourceUnderCursor();
            _run = new Run(this, target.Area, monitor, source);
            _run.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scrolling capture could not start");
            _tray.Notify("Scrolling capture failed", ex.Message);
        }
        finally
        {
            _choosing = false;
        }
    }

    /// <summary>One scrolling capture, from the panel appearing to the image being delivered.</summary>
    private sealed class Run
    {
        private readonly ScrollCaptureService _owner;
        private readonly PixelRect _area;
        private readonly MonitorInfo _monitor;
        private readonly CaptureSource _source;
        private readonly CancellationTokenSource _stop = new();
        private readonly ScrollAreaBorder _border;
        private readonly ScrollPanelWindow _window;
        private ScrollCaptureSession? _session;
        private Task? _loop;
        private DateTimeOffset _startedAt;
        private bool _finishing;

        public Run(ScrollCaptureService owner, PixelRect area, MonitorInfo monitor, CaptureSource source)
        {
            _owner = owner;
            _area = area;
            _monitor = monitor;
            _source = source;
            Panel = new ScrollPanelViewModel(
                () => BeginAsync(automatic: false),
                () => BeginAsync(automatic: true),
                () => FinishAsync(save: true),
                () => FinishAsync(save: false));
            _border = new ScrollAreaBorder(area, monitor);
            _window = new ScrollPanelWindow(Panel, area, monitor);
        }

        public ScrollPanelViewModel Panel { get; }

        public void Show()
        {
            _border.Show();
            _window.Show();
        }

        private Task BeginAsync(bool automatic)
        {
            if (_session is not null || _finishing)
            {
                return Task.CompletedTask;
            }

            Panel.SetState(automatic ? ScrollPanelState.Automatic : ScrollPanelState.Manual);
            _startedAt = _owner._time.GetLocalNow();
            var session = new ScrollCaptureSession(ScreenArea.Grabber(_area), _owner._time, tailRows: Math.Max(300, _area.Width * 3 / 2));
            _session = session;
            var progress = new Progress<ScrollProgress>(Panel.Apply); // created here, so it reports on the UI thread
            var token = _stop.Token;
            _loop = Task.Run(async () =>
            {
                if (automatic)
                {
                    var driver = ScreenScrollDriver.For(_area);
                    _owner._logger.LogInformation("Auto-scrolling {Area} with the {Method}", _area, driver.Method);
                    await session.RunAutomaticAsync(driver, progress, token);
                }
                else
                {
                    await session.RunManualAsync(progress, token);
                }
            });
            _ = FinishWhenDoneAsync(_loop);
            return Task.CompletedTask;
        }

        /// <summary>Automatic mode reaching the end, or either mode reaching the height cap, saves by itself.</summary>
        private async Task FinishWhenDoneAsync(Task loop)
        {
            try
            {
                await loop;
            }
            catch (Exception)
            {
                // FinishAsync awaits the loop again and reports the failure.
            }

            if (!_finishing)
            {
                await FinishAsync(save: true);
            }
        }

        /// <summary>Stops grabbing, closes the panel and frame, then saves the image (or drops it).</summary>
        public async Task FinishAsync(bool save)
        {
            if (_finishing)
            {
                return;
            }

            _finishing = true;
            Panel.SetState(ScrollPanelState.Finishing);
            await _stop.CancelAsync();
            Exception? failure = null;
            if (_loop is { } loop)
            {
                try
                {
                    await loop;
                }
                catch (Exception ex)
                {
                    failure = ex;
                    _owner._logger.LogError(ex, "Scrolling capture of {Area} failed", _area);
                }
            }

            _window.Close();
            _border.Close();
            _owner._run = null;
            _stop.Dispose();

            try
            {
                if (save && _session is { HasImage: true } session)
                {
                    var image = await Task.Run(session.Compose);
                    _owner._logger.LogInformation("Scrolling capture of {Area}: {Width}x{Height}", _area, image.Width, image.Height);
                    if (failure is not null)
                    {
                        _owner._tray.Notify("Scrolling capture stopped early", $"Saved what was captured. {failure.Message}");
                    }

                    await _owner._capture.DeliverAsync(image, _source, _startedAt, CaptureKind.Scrolling, _area, _monitor);
                }
                else if (failure is not null)
                {
                    _owner._tray.Notify("Scrolling capture failed", failure.Message);
                }
            }
            catch (Exception ex)
            {
                _owner._logger.LogError(ex, "Scrolling capture of {Area} could not be saved", _area);
                _owner._tray.Notify("Scrolling capture failed", ex.Message);
            }
        }
    }
}
