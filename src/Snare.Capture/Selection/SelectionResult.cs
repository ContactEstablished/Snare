using Snare.Core.Captures;
using Snare.Core.Geometry;

namespace Snare.Capture.Selection;

/// <summary>An accepted selection in physical pixels, with the window it was taken from, if any.</summary>
public sealed record SelectionResult(PixelRect Rect, CaptureTarget Target, WindowInfo? Window = null);
