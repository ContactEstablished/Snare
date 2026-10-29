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
        private const double PreviewDips = 480; // twice the panel's preview width, for sharpness

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
        private bool _closing;

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

            // Closing the panel some other way (Alt+F4) cancels the capture rather than leaving it running.
            _window.Closing += (_, e) =>
            {
                if (!_closing)
                {
                    e.Cancel = true;
                    _ = FinishAsync(save: false);
                }
            };
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
            var session = new ScrollCaptureSession(
                ScreenArea.Grabber(_area),
                _owner._time,
                tailRows: Math.Max(300, _area.Width * 3 / 2),
                previewWidth: (int)Math.Ceiling(PreviewDips * _monitor.Scale));
            _session = session;
            var progress = new Progress<ScrollProgress>(Panel.Apply); // created here, so it reports on the UI thread
            var token = _stop.Token;
            _loop = Task.Run(async () =>
            {
                if (automatic)
                {
                    var driver = ScreenScrollDriver.For(_area, _monitor.Scale);
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

        /// <summary>Automatic mode reaching the end, or either mode reaching the height cap, saves by itself and says why.</summary>
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
                await FinishAsync(save: true, endedByItself: true);
            }
        }

        /// <summary>
        /// Stops grabbing, then saves the image (or drops it). The panel stays up saying "Saving…"
        /// until the image is delivered, so a long save isn't silent and the hotkey can't start
        /// another capture meanwhile.
        /// </summary>
        public async Task FinishAsync(bool save, bool endedByItself = false)
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

            _stop.Dispose();
            _border.Close(); // the frame is gone before the quick bar appears
            try
            {
                var session = _session;
                _session = null;
                if (save && session is { HasImage: true })
                {
                    var reason = endedByItself ? session.EndReason : null;
                    var image = await Task.Run(session.Compose);
                    session = null; // let the stitched rows go before the save makes its copies
                    _owner._logger.LogInformation("Scrolling capture of {Area}: {Width}x{Height} ({Reason})", _area, image.Width, image.Height, reason ?? "stopped");
                    if (failure is not null)
                    {
                        _owner._tray.Notify("Scrolling capture stopped early", $"Saved what was captured. {failure.Message}");
                    }
                    else if (reason is not null)
                    {
                        _owner._tray.Notify("Scrolling capture saved", reason);
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
            finally
            {
                _closing = true;
                _window.Close();
                _owner._run = null;
            }
        }
    }
}
