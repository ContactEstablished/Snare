using Snare.Core.Imaging;

namespace Snare.Capture.Scrolling;

/// <summary>A snapshot for the live preview: the height so far, what the last frame did, and the bottom of the image.</summary>
public sealed record ScrollProgress(int Height, int MaxHeight, StitchOutcome Outcome, string? Warning, PixelBuffer? Tail);
