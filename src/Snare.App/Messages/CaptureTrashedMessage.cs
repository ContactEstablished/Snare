namespace Snare.App.Messages;

/// <summary>Sent on the UI thread after a capture moves to the Trash.</summary>
public sealed record CaptureTrashedMessage(Guid CaptureId);
