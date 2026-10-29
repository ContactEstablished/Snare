using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Snare.Core.Options;

namespace Snare.Recording.Ffmpeg;

/// <summary>Probe, thumbnail, trim and GIF export through FFmpeg. Everything is optional: without FFmpeg, recording still works.</summary>
public sealed class VideoTools(IOptionsMonitor<SnareOptions> options, FfmpegLocator locator, ILogger<VideoTools> logger)
{
    /// <summary>Where FFmpeg is, or null when it isn't installed.</summary>
    public string? FfmpegPath => locator.Find(options.CurrentValue.FfmpegPath);

    public bool IsAvailable => FfmpegPath is not null;

    public static string MissingMessage =>
        "FFmpeg wasn't found. Install it (winget install Gyan.FFmpeg), put ffmpeg.exe in tools\\ffmpeg next to Snare, or set FfmpegPath in appsettings.json.";

    public async Task<VideoInfo?> ProbeAsync(string video, CancellationToken cancellationToken = default)
    {
        if (FfmpegPath is not { } ffmpeg)
        {
            return null;
        }

        var output = await FfmpegRunner.RunAsync(ffmpeg, FfmpegArguments.Probe(video), allowFailure: true, cancellationToken);
        var (duration, size) = FfmpegArguments.ParseProbe(output);
        if (duration is null || size is null)
        {
            logger.LogWarning("FFmpeg couldn't read {Video}", video);
            return null;
        }

        return new VideoInfo(duration.Value, size.Value);
    }

    /// <summary>A PNG frame about 1 s in (or the first frame of a shorter video), or null.</summary>
    public async Task<byte[]?> ThumbnailPngAsync(string video, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (FfmpegPath is not { } ffmpeg)
        {
            return null;
        }

        var temp = Path.Combine(Path.GetTempPath(), $"snare-thumb-{Guid.NewGuid():N}.png");
        try
        {
            var at = duration > TimeSpan.FromSeconds(1.5) ? TimeSpan.FromSeconds(1) : TimeSpan.Zero;
            await FfmpegRunner.RunAsync(ffmpeg, FfmpegArguments.Thumbnail(video, temp, at), cancellationToken: cancellationToken);
            return File.Exists(temp) ? await File.ReadAllBytesAsync(temp, cancellationToken) : null;
        }
        catch (FfmpegException ex)
        {
            logger.LogWarning(ex, "No thumbnail for {Video}", video);
            return null;
        }
        finally
        {
            File.Delete(temp);
        }
    }

    /// <summary>Cuts the video to [start, end] in place: a stream copy to a temp file, then a replace.</summary>
    public async Task TrimInPlaceAsync(string video, TimeSpan start, TimeSpan end, CancellationToken cancellationToken = default)
    {
        var ffmpeg = FfmpegPath ?? throw new InvalidOperationException(MissingMessage);
        var temp = Path.Combine(Path.GetDirectoryName(video)!, $"{Path.GetFileNameWithoutExtension(video)}.trim-{Guid.NewGuid():N}{Path.GetExtension(video)}");
        try
        {
            await FfmpegRunner.RunAsync(ffmpeg, FfmpegArguments.Trim(video, temp, start, end), cancellationToken: cancellationToken);
            File.Move(temp, video, overwrite: true);
            logger.LogInformation("Trimmed {Video} to {Start}–{End}", video, start, end);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    public async Task ExportGifAsync(string video, string gif, TimeSpan? start, TimeSpan? end, int sourceWidth, CancellationToken cancellationToken = default)
    {
        var ffmpeg = FfmpegPath ?? throw new InvalidOperationException(MissingMessage);
        await FfmpegRunner.RunAsync(ffmpeg, FfmpegArguments.Gif(video, gif, start, end, sourceWidth), cancellationToken: cancellationToken);
        logger.LogInformation("Exported {Gif}", gif);
    }
}
