using System.IO;
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
/// <remarks>
/// Each take (the first, and each Restart) has its own cancellation token. Stop and Discard
/// cancel it, and every step of starting a take checks it is still the current session, so a
/// take can never start recording after the session that asked for it has ended.
/// </remarks>
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

    private bool _choosing;
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

        if (_choosing || overlay.IsOpen)
        {
            return;
        }

        _choosing = true;
        Session? session;
        try
        {
            session = await ChooseAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not set up the recording");
            tray.Notify("Recording failed", ex.Message);
            return;
        }
        finally
        {
            _choosing = false;
        }

        if (session is not null)
        {
            _session = session;
            await BeginTakeAsync(session);
        }
    }

    /// <summary>The target on a frozen screen, then the setup window. Null if Matt cancels.</summary>
    private async Task<Session?> ChooseAsync()
    {
        var desktop = freezer.Freeze();
        var selection = await overlay.SelectAsync(desktop);
        if (selection is null)
        {
            return null;
        }

        if (RecordingTargets.From(selection, desktop) is not { } target)
        {
            tray.Notify("Record video", "A recording covers one monitor. Pick a region, a window, or one monitor (F).");
            return null;
        }

        var monitor = desktop.Monitors.First(m => m.Monitor.DeviceName == target.DisplayDeviceName).Monitor;
        var source = selection.Window is { } w ? new CaptureSource(w.ProcessName, w.Title) : desktop.SourceUnderCursor();

        var setup = new RecordSetupViewModel(
            new PixelSize(target.Area.Width, target.Area.Height), recorder.Microphones(), recorder.Webcams(), settingsStore.Load());
        new RecordSetupWindow(setup).Show();
        if (await setup.Result is not { } settings)
        {
            return null;
        }

        settingsStore.Save(settings);
        return new Session(target, monitor, settings, source);
    }

    /// <summary>Countdown, then record to a fresh library path. Used for the first take and for Restart.</summary>
    private async Task BeginTakeAsync(Session session)
    {
        var take = session.NewTake();
        try
        {
            session.Controls?.SetStarting(true);
            await new CountdownWindow().RunAsync(CountdownSeconds, take);
            if (!IsCurrent(session, take))
            {
                return;
            }

            session.StartedAt = DateTimeOffset.Now;
            session.Path = store.NewVideoPath(session.StartedAt);
            await recorder.StartAsync(session.Path, session.Target, session.Settings, take);
            if (!IsCurrent(session, take))
            {
                // Stopped or discarded while the recorder was starting: don't keep recording.
                await recorder.DiscardAsync();
                return;
            }

            recorder.Failed += OnRecorderFailed;
            ShowOrResetControls(session);
        }
        catch (OperationCanceledException) when (take.IsCancellationRequested)
        {
            // Stop or Discard during the countdown or start.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not start recording");
            tray.Notify("Recording failed", ex.Message);
            if (IsCurrent(session, take))
            {
                EndSession(session);
            }
        }
        finally
        {
            session.Controls?.SetStarting(false);
        }
    }

    private void ShowOrResetControls(Session session)
    {
        if (session.Controls is { } existing)
        {
            // A restarted take is a new recorder: carry over mute and hidden webcam, or they'd be
            // recorded while the bar says they're off.
            existing.ResetClock();
            recorder.SetMicMuted(existing.IsMicMuted);
            recorder.SetWebcamVisible(!existing.IsWebcamHidden);
            return;
        }

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
            () => RestartAsync(session),
            () => DiscardAsync(session));
        session.Controls = controls;
        session.ControlBar = new RecordingControlBar(controls, session.Target.Area, session.Monitor);
        session.ControlBar.Show();
    }

    public async Task StopAsync()
    {
        if (_session is not { } session)
        {
            return;
        }

        var elapsed = session.Controls?.Elapsed ?? TimeSpan.Zero;
        EndSession(session);
        if (!recorder.IsRecording || session.Path is null)
        {
            return; // stopped during the countdown: nothing was recorded
        }

        string path;
        try
        {
            path = await recorder.StopAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The recorder didn't finish cleanly");
            await SaveWhatExistsAsync(session, elapsed);
            return;
        }

        await SaveAsync(session, path, elapsed, openPlayer: true);
    }

    private async Task RestartAsync(Session session)
    {
        if (!IsCurrent(session) || session.Controls?.IsStarting == true)
        {
            return;
        }

        recorder.Failed -= OnRecorderFailed;
        await recorder.DiscardAsync();
        if (IsCurrent(session))
        {
            await BeginTakeAsync(session);
        }
    }

    /// <summary>Stops the current recording and deletes it (tray → Exit → Discard).</summary>
    public Task DiscardAsync() => _session is { } session ? DiscardAsync(session) : Task.CompletedTask;

    private async Task DiscardAsync(Session session)
    {
        if (!IsCurrent(session))
        {
            return;
        }

        EndSession(session);
        if (recorder.IsRecording)
        {
            await recorder.DiscardAsync();
        }
    }

    /// <summary>Ends the session: cancels a starting take, closes the bar, stops listening for failures.</summary>
    private void EndSession(Session session)
    {
        if (ReferenceEquals(_session, session))
        {
            _session = null;
        }

        session.CancelTake();
        recorder.Failed -= OnRecorderFailed;
        session.ControlBar?.Close();
    }

    /// <summary>
    /// The recorder failed mid-take (a GPU or display change). It usually finalizes what it
    /// had, so keep that rather than throwing a long take away.
    /// </summary>
    private void OnRecorderFailed(object? sender, string error) =>
        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            if (_session is not { } session)
            {
                return;
            }

            var elapsed = session.Controls?.Elapsed ?? TimeSpan.Zero;
            EndSession(session);
            tray.Notify("Recording stopped", error);
            try
            {
                await recorder.StopAsync(); // releases the failed recorder; the file is kept
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "The failed recorder reported again on stop");
            }

            await SaveWhatExistsAsync(session, elapsed);
        });

    private async Task SaveWhatExistsAsync(Session session, TimeSpan elapsed)
    {
        if (session.Path is { } path && File.Exists(path) && new FileInfo(path).Length > 0)
        {
            await SaveAsync(session, path, elapsed, openPlayer: false);
        }
    }

    /// <summary>Adds the file to the library. FFmpeg trouble only costs the thumbnail and exact duration.</summary>
    private async Task SaveAsync(Session session, string path, TimeSpan elapsed, bool openPlayer)
    {
        VideoInfo? info = null;
        byte[]? thumbnail = null;
        try
        {
            info = await videoTools.ProbeAsync(path);
            thumbnail = await videoTools.ThumbnailPngAsync(path, info?.Duration ?? elapsed);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FFmpeg couldn't read {Path}; saving without a thumbnail", path);
        }

        try
        {
            var duration = info?.Duration ?? elapsed;
            var size = info?.Size ?? RecordingFrameSizeOf(session);
            var record = await store.SaveVideoAsync(
                path, size.Width, size.Height, (long)duration.TotalMilliseconds, session.Source, session.StartedAt, thumbnail);
            WeakReferenceMessenger.Default.Send(new CaptureSavedMessage(record));
            if (openPlayer)
            {
                player.Open(record);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add {Path} to the library", path);
            tray.Notify("Recording not added to the library", $"The file is at {path}. {ex.Message}");
        }
    }

    private static PixelSize RecordingFrameSizeOf(Session session) =>
        Snare.Recording.RecordingFrameSize.Fit(new PixelSize(session.Target.Area.Width, session.Target.Area.Height));

    private bool IsCurrent(Session session, CancellationToken? take = null) =>
        ReferenceEquals(_session, session) && take?.IsCancellationRequested != true;

    private sealed class Session(RecordingTarget target, MonitorInfo monitor, RecordingSettings settings, CaptureSource source)
    {
        private CancellationTokenSource? _take;

        public RecordingTarget Target { get; } = target;

        public MonitorInfo Monitor { get; } = monitor;

        public RecordingSettings Settings { get; } = settings;

        public CaptureSource Source { get; } = source;

        public DateTimeOffset StartedAt { get; set; }

        public string? Path { get; set; }

        public RecordingControlViewModel? Controls { get; set; }

        public RecordingControlBar? ControlBar { get; set; }

        public CancellationToken NewTake()
        {
            CancelTake();
            _take = new CancellationTokenSource();
            return _take.Token;
        }

        public void CancelTake()
        {
            _take?.Cancel();
            _take?.Dispose();
            _take = null;
        }
    }
}
