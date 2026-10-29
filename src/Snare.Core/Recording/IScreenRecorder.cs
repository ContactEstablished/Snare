namespace Snare.Core.Recording;

/// <summary>Records the screen to MP4. One recording at a time.</summary>
public interface IScreenRecorder
{
    bool IsRecording { get; }

    /// <summary>Raised (on any thread) if the recorder fails mid-recording, with its message.</summary>
    event EventHandler<string>? Failed;

    IReadOnlyList<MediaDevice> Microphones();

    IReadOnlyList<MediaDevice> Webcams();

    /// <summary>Starts recording to <paramref name="path"/>; completes once recording has begun.</summary>
    Task StartAsync(string path, RecordingTarget target, RecordingSettings settings, CancellationToken cancellationToken = default);

    void Pause();

    void Resume();

    void SetMicMuted(bool muted);

    void SetWebcamVisible(bool visible);

    /// <summary>Stops and finishes the file; returns its path.</summary>
    Task<string> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops and deletes the file.</summary>
    Task DiscardAsync(CancellationToken cancellationToken = default);
}
