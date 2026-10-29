using System.Globalization;
using System.Text.RegularExpressions;
using Snare.Core.Geometry;

namespace Snare.Recording.Ffmpeg;

/// <summary>FFmpeg argument lists (passed as separate arguments, never through a shell) and output parsing.</summary>
public static partial class FfmpegArguments
{
    public const int MaxGifWidth = 960;
    public const int GifFps = 12;

    /// <summary>`ffmpeg -i file` with no output: FFmpeg prints the duration and streams, then exits non-zero.</summary>
    public static IReadOnlyList<string> Probe(string input) => ["-hide_banner", "-i", input];

    public static IReadOnlyList<string> Thumbnail(string input, string outputPng, TimeSpan at) =>
        ["-hide_banner", "-y", "-ss", Seconds(at), "-i", input, "-frames:v", "1", "-vf", "scale=640:-2", outputPng];

    /// <summary>A stream copy: fast and lossless, so cuts snap to keyframes.</summary>
    public static IReadOnlyList<string> Trim(string input, string output, TimeSpan start, TimeSpan end)
    {
        if (end <= start)
        {
            throw new ArgumentException("The end must be after the start.", nameof(end));
        }

        return ["-hide_banner", "-y", "-ss", Seconds(start), "-to", Seconds(end), "-i", input, "-c", "copy", "-avoid_negative_ts", "make_zero", output];
    }

    /// <summary>A two-pass-quality GIF in one command: palettegen and paletteuse on a split stream.</summary>
    public static IReadOnlyList<string> Gif(string input, string output, TimeSpan? start, TimeSpan? end, int sourceWidth)
    {
        var width = Math.Min(MaxGifWidth, Math.Max(2, sourceWidth));
        var args = new List<string> { "-hide_banner", "-y" };
        if (start is { } s)
        {
            args.AddRange(["-ss", Seconds(s)]);
        }

        if (end is { } e)
        {
            args.AddRange(["-to", Seconds(e)]);
        }

        args.AddRange(
        [
            "-i", input,
            "-vf", string.Create(CultureInfo.InvariantCulture,
                $"fps={GifFps},scale={width}:-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse"),
            "-loop", "0",
            output,
        ]);
        return args;
    }

    /// <summary>Reads "Duration: 00:01:02.50" and the first video stream's "1920x1080" from FFmpeg's stderr.</summary>
    public static (TimeSpan? Duration, PixelSize? Size) ParseProbe(string stderr)
    {
        ArgumentNullException.ThrowIfNull(stderr);
        TimeSpan? duration = null;
        if (DurationPattern().Match(stderr) is { Success: true } d)
        {
            duration = new TimeSpan(0, int.Parse(d.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(d.Groups[2].Value, CultureInfo.InvariantCulture), 0)
                + TimeSpan.FromSeconds(double.Parse(d.Groups[3].Value, CultureInfo.InvariantCulture));
        }

        PixelSize? size = null;
        if (VideoSizePattern().Match(stderr) is { Success: true } v)
        {
            size = new PixelSize(int.Parse(v.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(v.Groups[2].Value, CultureInfo.InvariantCulture));
        }

        return (duration, size);
    }

    public static string Seconds(TimeSpan time) =>
        Math.Max(0, time.TotalSeconds).ToString("0.###", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"Duration:\s*(\d+):(\d{2}):(\d{2}(?:\.\d+)?)")]
    private static partial Regex DurationPattern();

    [GeneratedRegex(@"Stream #\S+.*?: Video: .*?(?<![\dx])(\d{2,5})x(\d{2,5})(?![\d])")]
    private static partial Regex VideoSizePattern();
}
