using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Snare.App.Messages;
using Snare.App.Services;
using Snare.Core.Captures;
using Snare.Recording.Ffmpeg;

namespace Snare.App.Video;

/// <summary>
/// The simple player: play, seek, mark a range, trim to it (in place) or export it as a GIF.
/// Playback itself is the view's MediaElement; this holds the range and runs FFmpeg.
/// </summary>
public sealed partial class VideoPlayerViewModel(CaptureRecord capture, VideoTools tools, ICaptureStore store, ILogger logger) : ObservableObject
{
    public CaptureRecord Capture { get; private set; } = capture;

    public string Title => $"Snare — {Path.GetFileName(Capture.FilePath)}";

    public bool CanUseFfmpeg => tools.IsAvailable;

    public string FfmpegHint => tools.IsAvailable ? "" : VideoTools.MissingMessage;

    /// <summary>Set by the view: the current playback position, for Set start / Set end.</summary>
    public Func<TimeSpan>? CurrentPosition { get; set; }

    /// <summary>Set by the view: releases the file before it is replaced, then reloads it.</summary>
    public Func<Func<Task>, Task>? WithMediaReleased { get; set; }

    /// <summary>Set by the view: confirms overwriting the video with the trimmed range.</summary>
    public Func<string, bool>? Confirm { get; set; }

    /// <summary>Set by the view: a path for the GIF, or null.</summary>
    public Func<string, string?>? PickGifPath { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangeText), nameof(HasRange))]
    public partial TimeSpan? RangeStart { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangeText), nameof(HasRange))]
    public partial TimeSpan? RangeEnd { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public TimeSpan Duration => TimeSpan.FromMilliseconds(Capture.DurationMs ?? 0);

    public bool HasRange => RangeStart is not null || RangeEnd is not null;

    public string RangeText => HasRange
        ? $"Range {Format(RangeStart ?? TimeSpan.Zero)} – {Format(RangeEnd ?? Duration)}"
        : "Whole video";

    public static string Format(TimeSpan t) => t.TotalHours >= 1
        ? t.ToString(@"h\:mm\:ss\.f", CultureInfo.InvariantCulture)
        : t.ToString(@"m\:ss\.f", CultureInfo.InvariantCulture);

    [RelayCommand]
    private void SetStart()
    {
        var at = CurrentPosition?.Invoke() ?? TimeSpan.Zero;
        RangeStart = at;
        if (RangeEnd is { } end && end <= at)
        {
            RangeEnd = null;
        }
    }

    [RelayCommand]
    private void SetEnd()
    {
        var at = CurrentPosition?.Invoke() ?? Duration;
        RangeEnd = at;
        if (RangeStart is { } start && start >= at)
        {
            RangeStart = null;
        }
    }

    [RelayCommand]
    private void ClearRange()
    {
        RangeStart = null;
        RangeEnd = null;
    }

    /// <summary>Keeps only the marked range, replacing the file (a stream copy, so cuts snap to keyframes).</summary>
    [RelayCommand]
    private async Task TrimAsync()
    {
        if (!tools.IsAvailable || !HasRange || IsBusy)
        {
            return;
        }

        var start = RangeStart ?? TimeSpan.Zero;
        var end = RangeEnd ?? Duration;
        if (Confirm?.Invoke($"Keep only {Format(start)} – {Format(end)} and delete the rest of this video? This can't be undone.") != true)
        {
            return;
        }

        await RunAsync("Trimming…", async () =>
        {
            async Task Trim()
            {
                await tools.TrimInPlaceAsync(Capture.FilePath, start, end);
                var info = await tools.ProbeAsync(Capture.FilePath);
                var duration = info?.Duration ?? (end - start);
                var thumbnail = await tools.ThumbnailPngAsync(Capture.FilePath, duration);
                Capture = await store.UpdateVideoAsync(Capture.Id, (long)duration.TotalMilliseconds, thumbnail);
            }

            if (WithMediaReleased is { } release)
            {
                await release(Trim);
            }
            else
            {
                await Trim();
            }

            ClearRange();
            OnPropertyChanged(nameof(Duration));
            WeakReferenceMessenger.Default.Send(new CaptureUpdatedMessage(Capture));
            return $"Trimmed to {Format(Duration)}";
        });
    }

    [RelayCommand]
    private async Task ExportGifAsync()
    {
        if (!tools.IsAvailable || IsBusy || PickGifPath?.Invoke(Path.GetFileNameWithoutExtension(Capture.FilePath)) is not { } gif)
        {
            return;
        }

        await RunAsync("Exporting GIF…", async () =>
        {
            await tools.ExportGifAsync(Capture.FilePath, gif, RangeStart, RangeEnd, Capture.Width);
            return $"Saved {Path.GetFileName(gif)}";
        });
    }

    [RelayCommand]
    private void Reveal() => ShellFiles.Reveal(Capture.FilePath);

    private async Task RunAsync(string busy, Func<Task<string>> work)
    {
        IsBusy = true;
        Status = busy;
        try
        {
            Status = await work();
        }
        catch (Exception ex) when (ex is FfmpegException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "{Action} failed for {Path}", busy, Capture.FilePath);
            Status = $"Failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
