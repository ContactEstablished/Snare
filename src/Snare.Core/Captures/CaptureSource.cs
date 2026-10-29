namespace Snare.Core.Captures;

/// <summary>The app and window a capture came from, saved for search.</summary>
public sealed record CaptureSource(string? App, string? WindowTitle)
{
    public static CaptureSource Unknown { get; } = new(null, null);
}
