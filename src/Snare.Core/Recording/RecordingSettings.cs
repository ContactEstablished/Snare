namespace Snare.Core.Recording;

/// <summary>Choices made before recording; remembered for next time.</summary>
/// <param name="MicrophoneId">Null records no mic.</param>
/// <param name="WebcamId">Null records no webcam.</param>
/// <param name="WebcamSize">Webcam height as a fraction of the video height (0.1 to 0.4).</param>
public sealed record RecordingSettings(
    string? MicrophoneId = null,
    bool SystemAudio = true,
    string? WebcamId = null,
    WebcamCorner WebcamCorner = WebcamCorner.BottomRight,
    double WebcamSize = 0.2,
    int FrameRate = 30,
    bool HighlightClicks = true)
{
    public const double MinWebcamSize = 0.1;
    public const double MaxWebcamSize = 0.4;

    /// <summary>The same settings with values outside their ranges pulled back in.</summary>
    public RecordingSettings Normalized() => this with
    {
        WebcamSize = Math.Clamp(WebcamSize, MinWebcamSize, MaxWebcamSize),
        FrameRate = FrameRate >= 45 ? 60 : 30,
    };
}
