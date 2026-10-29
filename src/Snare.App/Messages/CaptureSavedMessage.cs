using Snare.Core.Captures;

namespace Snare.App.Messages;

/// <summary>Sent on the UI thread after a new capture is in the library.</summary>
public sealed record CaptureSavedMessage(CaptureRecord Capture);
