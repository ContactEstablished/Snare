using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Snare.App.Capture;
using Snare.App.Messages;
using Snare.App.Services;
using Snare.App.Video;
using Snare.Capture.Selection;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Recording;
using Snare.Recording.Ffmpeg;

namespace Snare.App.Recording;

/// <summary>
/// The video flow: pick the target on a frozen screen, choose mic and webcam, count down, record
/// with a control bar that stays out of the video, then save to the library and open the player.
/// Shift+PrtSc starts it, and stops a recording in progress.
/// </summary>
public sealed class RecordingService(
    IScreenFreezer freezer,
    CaptureOverlay overlay,
    IScreenRecorder recorder,
    ICaptureStore store,
    VideoTools videoTools,
    RecordingSettingsStore settingsStore,
    VideoPlayerService player,
    TrayIconService tray,
    ILogger<RecordingService> logger)
{
    private const int CountdownSeconds = 3;

    private bool _busy;
    private Session? _session;

    public bool IsRecording => _session is not null;

    /// <summary>Shift+PrtSc and tray → Record video.</summary>
    public async Task ToggleAsync()
    {
        if (_session is not null)
        {
            await StopAsync();
            return;
        }

        if (_busy || overlay.IsOpen)
        {
            return;
        }

        _busy = true;
        try
        {
            await StartFlowAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not start recording");
            tray.Notify("Recording failed", ex.Message);
            await EndSessionAsync(discard: true);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task StartFlowAsync()
    {
        var desktop = freezer.Freeze();
        var selection = await overlay.SelectAsync(desktop);
        if (selection is null)
        {
            return;
        }

        if (RecordingTargets.From(selection, desktop) is not { } target)
        {
            tray.Notify("Record video", "A recording covers one monitor. Pick a region, a window, or one monitor (F).");
            return;
        }

        var monitor = desktop.Monitors.First(m => m.Monitor.DeviceName == target.DisplayDeviceName).Monitor;
        var source = selection.Window is { } w ? new CaptureSource(w.ProcessName, w.Title) : desktop.SourceUnderCursor();

        var setup = new RecordSetupViewModel(
            new PixelSize(target.Area.Width, target.Area.Height), recorder.Microphones(), recorder.Webcams(), settingsStore.Load());
        new RecordSetupWindow(setup).Show();
        if (await setup.Result is not { } settings)
        {
            return;
        }

        settingsStore.Save(settings);
        recorder.Failed += OnRecorderFailed;
        _session = new Session(target, monitor, settings, source);
        await BeginTakeAsync();
    }

    /// <summary>Countdown, then record to a fresh library path. Used for the first take and for Restart.</summary>
    private async Task BeginTakeAsync()
    {
        if (_session is not { } session)
        {
            return;
        }

        await new CountdownWindow().RunAsync(CountdownSeconds);
        session.StartedAt = DateTimeOffset.Now;
        session.Path = store.NewVideoPath(session.StartedAt);
        await recorder.StartAsync(session.Path, session.Target, session.Settings);

        if (session.ControlBar is null)
        {
            var controls = new RecordingControlViewModel(
                session.Settings.MicrophoneId is not null,
                session.Settings.WebcamId is not null,
                paused =>
                {
                    if (paused)
                    {
                        recorder.Pause();
                    }
                    else
                    {
                        recorder.Resume();
                    }
                },
                recorder.SetMicMuted,
                recorder.SetWebcamVisible,
                StopAsync,
                RestartAsync,
                DiscardAsync);
            session.Controls = controls;
            session.ControlBar = new RecordingControlBar(controls, session.Target.Area, session.Monitor);
            session.ControlBar.Show();
        }
        else
        {
            session.Controls?.ResetClock();
        }
    }

    public async Task StopAsync()
    {
        if (_session is not { } session || session.Path is null)
        {
            return;
        }

        var elapsed = session.Controls?.Elapsed ?? TimeSpan.Zero;
        session.ControlBar?.Close();
        _session = null;
        recorder.Failed -= OnRecorderFailed;

        try
        {
            var path = await recorder.StopAsync();
            var info = await videoTools.ProbeAsync(path);
            var duration = info?.Duration ?? elapsed;
            var size = info?.Size ?? new PixelSize(session.Target.Area.Width, session.Target.Area.Height);
            var thumbnail = await videoTools.ThumbnailPngAsync(path, duration);
            var record = await store.SaveVideoAsync(
                path, size.Width, size.Height, (long)duration.TotalMilliseconds, session.Source, session.StartedAt, thumbnail);
            WeakReferenceMessenger.Default.Send(new CaptureSavedMessage(record));
            player.Open(record);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not finish the recording");
            tray.Notify("Recording not saved", ex.Message);
        }
    }

    private async Task RestartAsync()
    {
        if (_session is null)
        {
            return;
        }

        await recorder.DiscardAsync();
        try
        {
            await BeginTakeAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not restart the recording");
            tray.Notify("Recording failed", ex.Message);
            await EndSessionAsync(discard: true);
        }
    }

    private Task DiscardAsync() => EndSessionAsync(discard: true);

    private async Task EndSessionAsync(bool discard)
    {
        var session = _session;
        _session = null;
        recorder.Failed -= OnRecorderFailed;
        session?.ControlBar?.Close();
        if (discard && recorder.IsRecording)
        {
            await recorder.DiscardAsync();
        }
    }

    private void OnRecorderFailed(object? sender, string error) =>
        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            tray.Notify("Recording failed", error);
            await EndSessionAsync(discard: true);
        });

    private sealed class Session(RecordingTarget target, MonitorInfo monitor, RecordingSettings settings, CaptureSource source)
    {
        public RecordingTarget Target { get; } = target;

        public MonitorInfo Monitor { get; } = monitor;

        public RecordingSettings Settings { get; } = settings;

        public CaptureSource Source { get; } = source;

        public DateTimeOffset StartedAt { get; set; }

        public string? Path { get; set; }

        public RecordingControlViewModel? Controls { get; set; }

        public RecordingControlBar? ControlBar { get; set; }
    }
}
