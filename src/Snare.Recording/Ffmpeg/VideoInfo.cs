using Snare.Core.Geometry;

namespace Snare.Recording.Ffmpeg;

/// <summary>What FFmpeg reports about a video file.</summary>
public sealed record VideoInfo(TimeSpan Duration, PixelSize Size);
