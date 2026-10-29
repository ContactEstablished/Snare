using ScreenRecorderLib;
using Snare.Core.Geometry;
using Snare.Core.Recording;

namespace Snare.Recording;

/// <summary>
/// Turns Snare's recording choices into ScreenRecorderLib options. Pure, so the mapping
/// (source, crop, output size, audio, webcam placement) is tested without recording anything.
/// </summary>
public static class RecordingOptionsFactory
{
    /// <summary>Gap between the webcam and the video's edges, in output pixels.</summary>
    public const int WebcamMargin = 24;

    /// <summary>Moving the webcam this far off the canvas hides it (there is no on/off switch).</summary>
    public static readonly ScreenSize HiddenOffset = new(-100_000, -100_000);

    public static RecordingPlan Create(RecordingTarget target, RecordingSettings settings, string? logPath = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        settings = settings.Normalized();

        var area = target.Area;
        var output = RecordingFrameSize.Fit(new PixelSize(area.Width, area.Height));

        RecordingSourceBase source = target.WindowHandle is { } handle
            ? new WindowRecordingSource(new IntPtr(handle)) { IsCursorCaptureEnabled = true }
            : new DisplayRecordingSource(target.DisplayDeviceName)
            {
                IsCursorCaptureEnabled = true,
                SourceRect = target.IsWholeDisplay
                    ? null
                    : new ScreenRect(target.AreaOnDisplay.X, target.AreaOnDisplay.Y, target.AreaOnDisplay.Width, target.AreaOnDisplay.Height),
            };

        var audio = new List<AudioSourceBase>();
        AudioSourceBase? microphone = null;
        if (settings.MicrophoneId is { } micId)
        {
            microphone = new CaptureAudioSource(micId) { Volume = 1.0f };
            audio.Add(microphone);
        }

        if (settings.SystemAudio)
        {
            audio.Add(new LoopbackAudioSource { Volume = 0.8f });
        }

        VideoCaptureOverlay? webcam = null;
        var offset = new ScreenSize(WebcamMargin, WebcamMargin);
        if (settings.WebcamId is { } camId)
        {
            webcam = new VideoCaptureOverlay
            {
                DeviceName = camId,
                AnchorPoint = AnchorFor(settings.WebcamCorner),
                Offset = offset,
                Size = new ScreenSize(0, Math.Max(2, (int)(output.Height * settings.WebcamSize))),
            };
        }

        var options = new RecorderOptions
        {
            SourceOptions = new SourceOptions { RecordingSources = [source] },
            OutputOptions = new OutputOptions
            {
                RecorderMode = RecorderMode.Video,
                OutputFrameSize = new ScreenSize(output.Width, output.Height),
                Stretch = StretchMode.Uniform,
            },
            AudioOptions = new AudioOptions
            {
                IsAudioEnabled = audio.Count > 0,
                AudioSources = audio,
                Bitrate = AudioBitrate.bitrate_192kbps,
                Channels = AudioChannels.Stereo,
            },
            VideoEncoderOptions = new VideoEncoderOptions
            {
                Framerate = settings.FrameRate,
                IsFixedFramerate = true,
                IsHardwareEncodingEnabled = true,
                IsMp4FastStartEnabled = true,
                Quality = 70,
                Encoder = new H264VideoEncoder
                {
                    BitrateMode = H264BitrateControlMode.Quality,
                    EncoderProfile = H264Profile.High,
                },
            },
            MouseOptions = new MouseOptions
            {
                IsMousePointerEnabled = true,
                IsMouseClicksDetected = settings.HighlightClicks,
                MouseLeftClickDetectionColor = "#FFFF00",
                MouseRightClickDetectionColor = "#00E5FF",
                MouseClickDetectionRadius = 28,
                MouseClickDetectionDuration = 150,
                MouseClickDetectionMode = MouseDetectionMode.Hook,
            },
            OverlayOptions = new OverLayOptions { Overlays = webcam is null ? [] : [webcam] },
            LogOptions = logPath is null
                ? new LogOptions { IsLogEnabled = false }
                : new LogOptions { IsLogEnabled = true, LogFilePath = logPath, LogSeverityLevel = LogLevel.Warn },
        };

        return new RecordingPlan(options, output, webcam, offset, microphone);
    }

    public static Anchor AnchorFor(WebcamCorner corner) => corner switch
    {
        WebcamCorner.TopLeft => Anchor.TopLeft,
        WebcamCorner.TopRight => Anchor.TopRight,
        WebcamCorner.BottomLeft => Anchor.BottomLeft,
        _ => Anchor.BottomRight,
    };
}
