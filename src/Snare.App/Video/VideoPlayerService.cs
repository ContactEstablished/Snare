using Microsoft.Extensions.Logging;
using Snare.App.Services;
using Snare.Core.Captures;
using Snare.Recording.Ffmpeg;

namespace Snare.App.Video;

/// <summary>Opens recordings in the player: one window per video, brought to the front if open.</summary>
public sealed class VideoPlayerService(VideoTools tools, ICaptureStore store, ClipboardService clipboard, ILogger<VideoPlayerService> logger)
{
    private readonly Dictionary<Guid, VideoPlayerWindow> _open = [];

    public void Open(CaptureRecord video)
    {
        ArgumentNullException.ThrowIfNull(video);
        if (_open.TryGetValue(video.Id, out var existing))
        {
            existing.Activate();
            return;
        }

        var window = new VideoPlayerWindow(new VideoPlayerViewModel(video, tools, store, clipboard, logger));
        _open[video.Id] = window;
        window.Closed += (_, _) => _open.Remove(video.Id);
        window.Show();
        window.Activate();
    }
}
