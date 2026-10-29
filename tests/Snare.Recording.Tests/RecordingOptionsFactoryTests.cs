using ScreenRecorderLib;
using Snare.Core.Geometry;
using Snare.Core.Recording;

namespace Snare.Recording.Tests;

public class RecordingOptionsFactoryTests
{
    private static readonly PixelRect Primary = new(0, 0, 5120, 1440);
    private static readonly PixelRect Upper = new(856, -1440, 3440, 1440);

    [Fact]
    public void WholePrimaryMonitor_IsScaledTo4096WideWithNoCrop()
    {
        var plan = RecordingOptionsFactory.Create(new RecordingTarget(@"\\.\DISPLAY1", Primary, Primary), new RecordingSettings());

        var source = Assert.IsType<DisplayRecordingSource>(Assert.Single(plan.Options.SourceOptions.RecordingSources));
        Assert.Equal(@"\\.\DISPLAY1", source.DeviceName);
        Assert.Null(source.SourceRect);
        Assert.Equal(new PixelSize(4096, 1152), plan.OutputSize);
        Assert.Equal((4096, 1152), (plan.Options.OutputOptions.OutputFrameSize.Width, plan.Options.OutputOptions.OutputFrameSize.Height));
    }

    [Fact]
    public void RegionOnTheUpperMonitor_IsCroppedRelativeToThatMonitor()
    {
        var area = new PixelRect(1000, -1000, 801, 600);

        var plan = RecordingOptionsFactory.Create(new RecordingTarget(@"\\.\DISPLAY2", Upper, area), new RecordingSettings());

        var source = Assert.IsType<DisplayRecordingSource>(Assert.Single(plan.Options.SourceOptions.RecordingSources));
        Assert.Equal((144, 440, 801, 600), (source.SourceRect.Left, source.SourceRect.Top, source.SourceRect.Width, source.SourceRect.Height));
        Assert.Equal(new PixelSize(800, 600), plan.OutputSize); // even width for H.264
    }

    [Fact]
    public void Window_RecordsByHandle()
    {
        var plan = RecordingOptionsFactory.Create(
            new RecordingTarget(@"\\.\DISPLAY1", Primary, new PixelRect(100, 100, 1200, 800), WindowHandle: 0x1234), new RecordingSettings());

        var source = Assert.IsType<WindowRecordingSource>(Assert.Single(plan.Options.SourceOptions.RecordingSources));
        Assert.Equal(new IntPtr(0x1234), source.Handle);
    }

    [Fact]
    public void Audio_IsMicAndOrSystemAsChosen()
    {
        var target = new RecordingTarget(@"\\.\DISPLAY1", Primary, Primary);

        var both = RecordingOptionsFactory.Create(target, new RecordingSettings(MicrophoneId: "mic-1", SystemAudio: true));
        var none = RecordingOptionsFactory.Create(target, new RecordingSettings(MicrophoneId: null, SystemAudio: false));

        Assert.True(both.Options.AudioOptions.IsAudioEnabled);
        Assert.Equal(2, both.Options.AudioOptions.AudioSources.Count);
        Assert.IsType<CaptureAudioSource>(both.Microphone);
        Assert.False(none.Options.AudioOptions.IsAudioEnabled);
        Assert.Null(none.Microphone);
    }

    [Theory]
    [InlineData(WebcamCorner.BottomRight, Anchor.BottomRight)]
    [InlineData(WebcamCorner.TopLeft, Anchor.TopLeft)]
    public void Webcam_IsAnOverlayInTheChosenCornerAtTheChosenSize(WebcamCorner corner, Anchor anchor)
    {
        var plan = RecordingOptionsFactory.Create(
            new RecordingTarget(@"\\.\DISPLAY1", Primary, Primary),
            new RecordingSettings(WebcamId: "cam-1", WebcamCorner: corner, WebcamSize: 0.25));

        var webcam = Assert.IsType<VideoCaptureOverlay>(Assert.Single(plan.Options.OverlayOptions.Overlays));
        Assert.Same(webcam, plan.Webcam);
        Assert.Equal("cam-1", webcam.DeviceName);
        Assert.Equal(anchor, webcam.AnchorPoint);
        Assert.Equal(288, webcam.Size.Height); // a quarter of 1152
        Assert.Equal(RecordingOptionsFactory.WebcamMargin, webcam.Offset.Width);
    }

    [Fact]
    public void NoWebcam_MeansNoOverlay()
    {
        var plan = RecordingOptionsFactory.Create(new RecordingTarget(@"\\.\DISPLAY1", Primary, Primary), new RecordingSettings(WebcamId: null));

        Assert.Empty(plan.Options.OverlayOptions.Overlays);
        Assert.Null(plan.Webcam);
    }

    [Fact]
    public void Settings_AreNormalized()
    {
        var plan = RecordingOptionsFactory.Create(
            new RecordingTarget(@"\\.\DISPLAY1", Primary, Primary),
            new RecordingSettings(WebcamId: "cam", WebcamSize: 0.9, FrameRate: 50, HighlightClicks: false));

        Assert.Equal(60, plan.Options.VideoEncoderOptions.Framerate);
        Assert.Equal((int)(1152 * RecordingSettings.MaxWebcamSize), plan.Webcam!.Size.Height);
        Assert.False(plan.Options.MouseOptions.IsMouseClicksDetected);
        Assert.True(plan.Options.VideoEncoderOptions.IsHardwareEncodingEnabled);
    }
}
