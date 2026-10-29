using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Snare.App.Recording;
using Snare.App.Services;
using Snare.App.Video;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Options;
using Snare.Core.Recording;
using Snare.Recording.Ffmpeg;

namespace Snare.App.Tests;

public class VideoUiTests
{
    private static readonly MediaDevice Mic = new("mic-1", "fifine Microphone", true);
    private static readonly MediaDevice OtherMic = new("mic-2", "EMEET mic", false);
    private static readonly MediaDevice Cam = new("cam-1", "EMEET SmartCam C960 4K", true);

    private static VideoTools NoFfmpeg() =>
        new(new StaticOptions(), new FfmpegLocator(_ => false, null, @"C:\nowhere"), NullLogger<VideoTools>.Instance);

    private static CaptureRecord Video() => FakeCaptureStore.Record(1) with { Kind = CaptureKind.Video, DurationMs = 20_000 };

    [Fact]
    public void Setup_FirstTime_PicksTheDefaultMicAndNoWebcam()
    {
        var setup = new RecordSetupViewModel(new PixelSize(5120, 1440), [OtherMic, Mic], [Cam], last: null);

        Assert.Equal(Mic, setup.SelectedMicrophone);
        Assert.Equal(RecordSetupViewModel.None, setup.SelectedWebcam);
        Assert.Equal("4096 × 1152 (scaled from 5120 × 1440) · H.264 MP4", setup.OutputText);
        Assert.Equal(new RecordingSettings(MicrophoneId: "mic-1", SystemAudio: true, WebcamId: null), setup.ToSettings());
    }

    [Fact]
    public void Setup_RestoresLastChoices_WhenTheDevicesStillExist()
    {
        var last = new RecordingSettings("mic-2", false, "cam-1", WebcamCorner.TopLeft, 0.3, 60, false);

        var setup = new RecordSetupViewModel(new PixelSize(800, 600), [Mic, OtherMic], [Cam], last);

        Assert.Equal(last, setup.ToSettings());
        Assert.True(setup.HasWebcam);
    }

    [Fact]
    public void Setup_ForgetsDevicesThatAreGone()
    {
        var setup = new RecordSetupViewModel(new PixelSize(800, 600), [Mic], [], new RecordingSettings("unplugged", true, "unplugged-cam"));

        Assert.Equal(RecordSetupViewModel.None, setup.SelectedMicrophone);
        Assert.Equal(RecordSetupViewModel.None, setup.SelectedWebcam);
    }

    [Fact]
    public async Task Setup_RecordAndCancelCompleteTheResult()
    {
        var record = new RecordSetupViewModel(new PixelSize(800, 600), [Mic], [Cam], null);
        record.SetCornerCommand.Execute(WebcamCorner.TopRight);
        record.RecordCommand.Execute(null);
        Assert.Equal(WebcamCorner.TopRight, (await record.Result)!.WebcamCorner);

        var cancel = new RecordSetupViewModel(new PixelSize(800, 600), [Mic], [Cam], null);
        cancel.Cancel();
        Assert.Null(await cancel.Result);
    }

    [Fact]
    public async Task ControlBar_TogglesReachTheRecorder()
    {
        var calls = new List<string>();
        var controls = new RecordingControlViewModel(
            hasMicrophone: true, hasWebcam: true,
            paused => calls.Add($"paused={paused}"),
            muted => calls.Add($"muted={muted}"),
            visible => calls.Add($"webcam={visible}"),
            () => { calls.Add("stop"); return Task.CompletedTask; },
            () => { calls.Add("restart"); return Task.CompletedTask; },
            () => { calls.Add("discard"); return Task.CompletedTask; });

        controls.TogglePauseCommand.Execute(null);
        Assert.Equal("▶ Resume", controls.PauseLabel);
        controls.TogglePauseCommand.Execute(null);
        controls.ToggleMicCommand.Execute(null);
        controls.ToggleWebcamCommand.Execute(null);
        await controls.StopCommand.ExecuteAsync(null);

        Assert.Equal(["paused=True", "paused=False", "muted=True", "webcam=False", "stop"], calls);
        Assert.Equal("Show webcam", controls.WebcamLabel);
    }

    [Fact]
    public void ControlBar_RestartIsOffWhileATakeIsStarting()
    {
        var controls = new RecordingControlViewModel(true, true, _ => { }, _ => { }, _ => { }, () => Task.CompletedTask, () => Task.CompletedTask, () => Task.CompletedTask);

        controls.SetStarting(true);
        Assert.False(controls.RestartCommand.CanExecute(null));
        Assert.True(controls.StopCommand.CanExecute(null));
        Assert.True(controls.DiscardCommand.CanExecute(null));

        controls.SetStarting(false);
        Assert.True(controls.RestartCommand.CanExecute(null));
    }

    [Fact]
    public async Task Player_TrimRangePastTheEnd_IsRejectedNotStuck()
    {
        var tools = new VideoTools(new StaticOptions(), new FfmpegLocator(_ => true, null, @"C:\fake"), NullLogger<VideoTools>.Instance);
        var player = new VideoPlayerViewModel(Video(), tools, new FakeCaptureStore(), new ClipboardService(NullLogger<ClipboardService>.Instance), NullLogger.Instance)
        {
            CurrentPosition = () => TimeSpan.FromSeconds(25), // past the 20 s duration
            Confirm = _ => throw new InvalidOperationException("should not ask"),
        };
        player.SetStartCommand.Execute(null);

        await player.TrimCommand.ExecuteAsync(null);

        Assert.False(player.IsBusy);
        Assert.Equal("Pick a range with the end after the start.", player.Status);
    }

    [Fact]
    public void Player_RangeMarksAndClears()
    {
        var player = new VideoPlayerViewModel(Video(), NoFfmpeg(), new FakeCaptureStore(), new ClipboardService(NullLogger<ClipboardService>.Instance), NullLogger.Instance);
        var now = TimeSpan.FromSeconds(5);
        player.CurrentPosition = () => now;

        player.SetStartCommand.Execute(null);
        now = TimeSpan.FromSeconds(12.5);
        player.SetEndCommand.Execute(null);
        Assert.Equal("Range 0:05.0 – 0:12.5", player.RangeText);

        now = TimeSpan.FromSeconds(15);
        player.SetStartCommand.Execute(null); // after the end: the end is dropped
        Assert.Null(player.RangeEnd);

        player.ClearRangeCommand.Execute(null);
        Assert.Equal("Whole video", player.RangeText);
    }

    [Fact]
    public void Player_WithoutFfmpeg_ExplainsAndDisablesTrim()
    {
        var player = new VideoPlayerViewModel(Video(), NoFfmpeg(), new FakeCaptureStore(), new ClipboardService(NullLogger<ClipboardService>.Instance), NullLogger.Instance);

        Assert.False(player.CanUseFfmpeg);
        Assert.Contains("winget install Gyan.FFmpeg", player.FfmpegHint, StringComparison.Ordinal);
    }

    [Fact]
    public void Windows_LoadTheirXaml()
    {
        Sta.Run(() =>
        {
            var setup = new RecordSetupWindow(new RecordSetupViewModel(new PixelSize(800, 600), [Mic], [Cam], null));
            Assert.Equal("Record video", setup.Title);

            var bounds = new PixelRect(0, 0, 1920, 1080);
            var controls = new RecordingControlViewModel(true, false, _ => { }, _ => { }, _ => { }, () => Task.CompletedTask, () => Task.CompletedTask, () => Task.CompletedTask);
            var bar = new RecordingControlBar(controls, new PixelRect(100, 100, 800, 600), new MonitorInfo("m", bounds, bounds, 96, true));
            Assert.Same(controls, bar.DataContext);

            var playerModel = new VideoPlayerViewModel(Video(), NoFfmpeg(), new FakeCaptureStore(), new ClipboardService(NullLogger<ClipboardService>.Instance), NullLogger.Instance);
            var player = new VideoPlayerWindow(playerModel);
            Assert.Same(playerModel, player.DataContext);
            Assert.StartsWith("Snare — ", playerModel.Title, StringComparison.Ordinal);
        });
    }

    private sealed class StaticOptions : IOptionsMonitor<SnareOptions>
    {
        public SnareOptions CurrentValue { get; } = new();

        public SnareOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<SnareOptions, string?> listener) => null;
    }
}
