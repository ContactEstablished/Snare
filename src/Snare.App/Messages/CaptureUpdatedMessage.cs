using Snare.Core.Captures;

namespace Snare.App.Messages;

/// <summary>Sent on the UI thread after a capture is re-exported or its original discarded.</summary>
public sealed record CaptureUpdatedMessage(CaptureRecord Capture);
