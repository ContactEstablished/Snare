using System.Globalization;
using Microsoft.Extensions.Logging;
using ScreenRecorderLib;
using Snare.Core.Geometry;
using Snare.Interop;

namespace Snare.Recording;

/// <summary>
/// Phase 0 spike: proves ScreenRecorderLib can record the primary monitor with mic, system audio,
/// a webcam overlay and click highlighting, and can pause, hide the webcam and mute the mic
/// mid-recording. Phase 4 replaces this with the real recorder.
/// </summary>
public sealed class RecordingSpike(ILogger<RecordingSpike> logger)
{
    private static readonly ScreenSize HiddenOffset = new(-100_000, -100_000);

    /// <summary>Runs the scripted ~22 s recording and returns the MP4 path.</summary>
    public async Task<string> RunAsync(string outputFolder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);
        Directory.CreateDirectory(outputFolder);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var videoPath = Path.Combine(outputFolder, $"recording-spike-{stamp}.mp4");
        var logPath = Path.Combine(outputFolder, $"recording-spike-{stamp}.log");

        var primary = DisplayMonitors.GetAll().First(m => m.IsPrimary);
        var output = RecordingFrameSize.Fit(new PixelSize(primary.Bounds.Width, primary.Bounds.Height));

        var cameras = Recorder.GetSystemVideoCaptureDevices();
        var camera = cameras.FirstOrDefault();
        var microphone = Recorder.GetSystemAudioCaptureDevices().FirstOrDefault(d => d.IsDefaultDevice);
        var speakers = Recorder.GetSystemAudioLoopbackDevices().FirstOrDefault(d => d.IsDefaultDevice);
        logger.LogInformation(
            "Recording spike: {Device} {Width}x{Height} -> {OutWidth}x{OutHeight}; camera {Camera}; mic {Mic}; loopback {Loopback}",
            primary.DeviceName, primary.Bounds.Width, primary.Bounds.Height, output.Width, output.Height,
            camera?.FriendlyName ?? "none", microphone?.FriendlyName ?? "none", speakers?.FriendlyName ?? "none");

        var webcamOffset = new ScreenSize(24, 24);
        var webcam = new VideoCaptureOverlay
        {
            AnchorPoint = Anchor.BottomRight,
            Offset = webcamOffset,
            Size = new ScreenSize(0, (int)(output.Height * 0.2)),
            DeviceName = camera?.DeviceName,
        };

        var mic = microphone is not null ? (AudioSourceBase)microphone : CaptureAudioSource.Default;
        var loopback = speakers is not null ? (AudioSourceBase)speakers : LoopbackAudioSource.Default;
        mic.Volume = 1.0f;
        loopback.Volume = 0.6f;

        var options = new RecorderOptions
        {
            SourceOptions = new SourceOptions
            {
                RecordingSources = [new DisplayRecordingSource(primary.DeviceName)],
            },
            OutputOptions = new OutputOptions
            {
                RecorderMode = RecorderMode.Video,
                OutputFrameSize = new ScreenSize(output.Width, output.Height),
                Stretch = StretchMode.Uniform,
            },
            AudioOptions = new AudioOptions
            {
                IsAudioEnabled = true,
                AudioSources = [mic, loopback],
                Bitrate = AudioBitrate.bitrate_192kbps,
                Channels = AudioChannels.Stereo,
            },
            VideoEncoderOptions = new VideoEncoderOptions
            {
                Framerate = 30,
                IsFixedFramerate = true,
                IsHardwareEncodingEnabled = true,
                Encoder = new H264VideoEncoder
                {
                    BitrateMode = H264BitrateControlMode.Quality,
                    EncoderProfile = H264Profile.High,
                },
                Quality = 70,
                IsMp4FastStartEnabled = true,
            },
            MouseOptions = new MouseOptions
            {
                IsMousePointerEnabled = true,
                IsMouseClicksDetected = true,
                MouseLeftClickDetectionColor = "#FFFF00",
                MouseRightClickDetectionColor = "#00E5FF",
                MouseClickDetectionRadius = 30,
                MouseClickDetectionDuration = 150,
                MouseClickDetectionMode = MouseDetectionMode.Hook,
            },
            OverlayOptions = new OverLayOptions
            {
                Overlays = camera is null ? [] : [webcam],
            },
            LogOptions = new LogOptions
            {
                IsLogEnabled = true,
                LogFilePath = logPath,
                LogSeverityLevel = ScreenRecorderLib.LogLevel.Debug,
            },
        };

        var finished = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var recorder = Recorder.CreateRecorder(options);
        recorder.OnRecordingComplete += (_, e) => finished.TrySetResult(e.FilePath);
        recorder.OnRecordingFailed += (_, e) => finished.TrySetException(new InvalidOperationException($"Recording failed: {e.Error}"));
        recorder.OnStatusChanged += (_, e) => logger.LogInformation("Recorder status {Status}", e.Status);

        recorder.Record(videoPath);
        try
        {
            await Step(5, "pause", recorder.Pause, finished.Task, cancellationToken);
            await Step(3, "resume", recorder.Resume, finished.Task, cancellationToken);
            await Step(3, "hide webcam", () => MoveWebcam(recorder, webcam, HiddenOffset), finished.Task, cancellationToken);
            await Step(3, "show webcam", () => MoveWebcam(recorder, webcam, webcamOffset), finished.Task, cancellationToken);
            await Step(1, "mute mic", () => SetVolume(recorder, mic, 0), finished.Task, cancellationToken);
            await Step(3, "unmute mic", () => SetVolume(recorder, mic, 1.0f), finished.Task, cancellationToken);
            await Step(4, "stop", recorder.Stop, finished.Task, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            recorder.Stop();
            throw;
        }

        var path = await finished.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        logger.LogInformation("Recording spike saved {Path}", path);
        return path;
    }

    /// <summary>Waits, then runs one script step, unless the recorder has already ended or failed.</summary>
    private async Task Step(
        int afterSeconds, string name, Action action, Task<string> finished, CancellationToken cancellationToken)
    {
        await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(afterSeconds), cancellationToken), finished);
        cancellationToken.ThrowIfCancellationRequested();
        if (finished.IsCompleted)
        {
            await finished; // rethrows the recorder's own failure
            throw new InvalidOperationException($"The recording ended before the \"{name}\" step.");
        }

        logger.LogInformation("Recording spike step: {Step}", name);
        action();
    }

    // There is no overlay on/off switch, so the spike tests hiding it by moving it off the canvas.
    private static void MoveWebcam(Recorder recorder, VideoCaptureOverlay webcam, ScreenSize offset)
    {
        webcam.Offset = offset;
        recorder.GetDynamicOptionsBuilder().SetUpdatedOverlay(webcam).Apply();
    }

    private static void SetVolume(Recorder recorder, AudioSourceBase source, float volume)
    {
        source.Volume = volume;
        recorder.GetDynamicOptionsBuilder().SetUpdatedAudioSource(source).Apply();
    }
}
