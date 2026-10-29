using ScreenRecorderLib;
using Snare.Core.Geometry;

namespace Snare.Recording;

/// <summary>
/// Recorder options plus the pieces changed while recording (the webcam overlay and the mic),
/// which ScreenRecorderLib updates by reference.
/// </summary>
public sealed record RecordingPlan(
    RecorderOptions Options,
    PixelSize OutputSize,
    VideoCaptureOverlay? Webcam,
    ScreenSize WebcamOffset,
    AudioSourceBase? Microphone);
