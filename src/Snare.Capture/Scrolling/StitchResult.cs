namespace Snare.Capture.Scrolling;

/// <summary>The outcome of one frame, how far the page moved, and a warning for the preview when a frame was dropped.</summary>
public readonly record struct StitchResult(StitchOutcome Outcome, int Offset = 0, double Confidence = 0, string? Warning = null);
