using Microsoft.Extensions.Logging;
using ScreenRecorderLib;
using Snare.Core.Recording;

namespace Snare.Recording;

/// <summary>
/// <see cref="IScreenRecorder"/> on ScreenRecorderLib (Media Foundation, NVENC on Matt's PC).
/// Webcam hiding and mic muting go through dynamic options, as tried in the Phase 0 spike.
/// </summary>
public sealed class ScreenRecorderLibRecorder(ILogger<ScreenRecorderLibRecorder> logger) : IScreenRecorder, IDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(60);

    private Recorder? _recorder;
    private RecordingPlan? _plan;
    private string? _path;
    private TaskCompletionSource<string>? _completed;

    public bool IsRecording => _recorder is not null;

    public event EventHandler<string>? Failed;

    public IReadOnlyList<MediaDevice> Microphones() =>
        Recorder.GetSystemAudioCaptureDevices()
            .Select(d => new MediaDevice(d.DeviceName, d.FriendlyName, d.IsDefaultDevice))
            .ToList();

    public IReadOnlyList<MediaDevice> Webcams() =>
        Recorder.GetSystemVideoCaptureDevices()
            .Select((d, i) => new MediaDevice(d.DeviceName, d.FriendlyName, i == 0))
            .ToList();

    public async Task StartAsync(string path, RecordingTarget target, RecordingSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (_recorder is not null)
        {
            throw new InvalidOperationException("A recording is already running.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // The recorder log goes to %TEMP%, never into the library folder.
        var plan = RecordingOptionsFactory.Create(target, settings, System.IO.Path.Combine(System.IO.Path.GetTempPath(), "snare-recorder.log"));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var recorder = Recorder.CreateRecorder(plan.Options);
        recorder.OnStatusChanged += (_, e) =>
        {
            if (e.Status == RecorderStatus.Recording)
            {
                started.TrySetResult();
            }
        };
        recorder.OnRecordingComplete += (_, e) => completed.TrySetResult(e.FilePath);
        recorder.OnRecordingFailed += (_, e) =>
        {
            var error = new InvalidOperationException($"Recording failed: {e.Error}");
            started.TrySetException(error);
            if (completed.TrySetException(error))
            {
                logger.LogError("Recording failed: {Error}", e.Error);
                Failed?.Invoke(this, e.Error);
            }
        };

        _recorder = recorder;
        _plan = plan;
        _path = path;
        _completed = completed;

        recorder.Record(path);
        try
        {
            await started.Task.WaitAsync(StartTimeout, cancellationToken);
        }
        catch
        {
            await DiscardAsync(CancellationToken.None);
            throw;
        }

        logger.LogInformation("Recording {Target} at {Width}x{Height} to {Path}", target.Area, plan.OutputSize.Width, plan.OutputSize.Height, path);
    }

    public void Pause() => _recorder?.Pause();

    public void Resume() => _recorder?.Resume();

    public void SetMicMuted(bool muted)
    {
        if (_recorder is null || _plan?.Microphone is not { } mic)
        {
            return;
        }

        mic.Volume = muted ? 0f : 1f;
        _recorder.GetDynamicOptionsBuilder().SetUpdatedAudioSource(mic).Apply();
    }

    public void SetWebcamVisible(bool visible)
    {
        if (_recorder is null || _plan?.Webcam is not { } webcam)
        {
            return;
        }

        webcam.Offset = visible ? _plan.WebcamOffset : RecordingOptionsFactory.HiddenOffset;
        _recorder.GetDynamicOptionsBuilder().SetUpdatedOverlay(webcam).Apply();
    }

    public async Task<string> StopAsync(CancellationToken cancellationToken = default)
    {
        if (_recorder is not { } recorder || _completed is not { } completed)
        {
            throw new InvalidOperationException("No recording is running.");
        }

        try
        {
            recorder.Stop();
            return await completed.Task.WaitAsync(StopTimeout, cancellationToken);
        }
        finally
        {
            Release();
        }
    }

    public async Task DiscardAsync(CancellationToken cancellationToken = default)
    {
        var path = _path;
        if (_recorder is { } recorder)
        {
            try
            {
                recorder.Stop();
                if (_completed is { } completed)
                {
                    await completed.Task.WaitAsync(StopTimeout, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
            {
                logger.LogDebug(ex, "The discarded recording ended with an error");
            }
            finally
            {
                Release();
            }
        }

        if (path is not null)
        {
            TryDelete(path);
        }
    }

    public void Dispose() => Release();

    private void Release()
    {
        _recorder?.Dispose();
        _recorder = null;
        _plan = null;
        _completed = null;
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not delete {Path}", path);
        }
    }
}
