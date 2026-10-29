using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Snare.Core.Geometry;
using Snare.Core.Options;
using Snare.Recording.Ffmpeg;

namespace Snare.Recording.Tests;

public class FfmpegTests
{
    private const string SampleProbe = """
        Input #0, mov,mp4,m4a,3gp,3g2,mj2, from 'C:\x.mp4':
          Metadata:
            major_brand     : isom
          Duration: 00:01:02.48, start: 0.000000, bitrate: 2210 kb/s
          Stream #0:0[0x1](und): Video: h264 (High) (avc1 / 0x31637661), yuv420p(tv, bt709, progressive), 4096x1152 [SAR 1:1 DAR 32:9], 2010 kb/s, 30 fps, 30 tbr, 15360 tbn (default)
          Stream #0:1[0x2](und): Audio: aac (LC) (mp4a / 0x6134706D), 48000 Hz, stereo, fltp, 192 kb/s (default)
        At least one output file must be specified
        """;

    [Fact]
    public void ParseProbe_ReadsDurationAndVideoSize()
    {
        var (duration, size) = FfmpegArguments.ParseProbe(SampleProbe);

        Assert.Equal(TimeSpan.FromSeconds(62.48), duration);
        Assert.Equal(new PixelSize(4096, 1152), size);
    }

    [Fact]
    public void ParseProbe_OfGarbage_IsEmpty()
    {
        Assert.Equal((null, null), FfmpegArguments.ParseProbe("nothing useful"));
    }

    [Fact]
    public void Trim_IsAStreamCopyWithInvariantSeconds()
    {
        var args = FfmpegArguments.Trim("in.mp4", "out.mp4", TimeSpan.FromSeconds(1.25), TimeSpan.FromMinutes(1.5));

        Assert.Equal(["-hide_banner", "-y", "-ss", "1.25", "-to", "90", "-i", "in.mp4", "-c", "copy", "-avoid_negative_ts", "make_zero", "out.mp4"], args);
    }

    [Fact]
    public void Trim_RejectsAnEmptyRange()
    {
        Assert.Throws<ArgumentException>(() => FfmpegArguments.Trim("a", "b", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Gif_CapsTheWidthAndUsesAPalette()
    {
        var args = FfmpegArguments.Gif("in.mp4", "out.gif", null, TimeSpan.FromSeconds(3), sourceWidth: 4096);

        Assert.DoesNotContain("-ss", args);
        Assert.Contains("fps=12,scale=960:-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse", args);
        Assert.Equal("out.gif", args[^1]);
    }

    [Fact]
    public void Locator_PrefersSettingsThenBundledThenPath()
    {
        var exists = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"D:\custom\ffmpeg.exe",
            @"C:\Snare\tools\ffmpeg\ffmpeg.exe",
            @"C:\winget\ffmpeg.exe",
        };
        var locator = new FfmpegLocator(exists.Contains, @"C:\nothing;C:\winget", @"C:\Snare");

        Assert.Equal(@"D:\custom\ffmpeg.exe", locator.Find(@"D:\custom\ffmpeg.exe"));
        Assert.Equal(@"C:\Snare\tools\ffmpeg\ffmpeg.exe", locator.Find(@"D:\missing\ffmpeg.exe"));
        exists.Remove(@"C:\Snare\tools\ffmpeg\ffmpeg.exe");
        Assert.Equal(@"C:\winget\ffmpeg.exe", locator.Find(null));
        exists.Clear();
        Assert.Null(locator.Find(null));
    }

    // ---- Real FFmpeg on a generated test video; run with Snare.Recording.Tests.exe -explicit only ----

    [Fact(Explicit = true)]
    public async Task RealFfmpeg_ProbesThumbnailsTrimsAndExportsGif()
    {
        var tools = new VideoTools(new StaticOptions(), FfmpegLocator.ForThisMachine(), NullLogger<VideoTools>.Instance);
        Assert.True(tools.IsAvailable, VideoTools.MissingMessage);
        var ct = TestContext.Current.CancellationToken;
        var folder = Directory.CreateTempSubdirectory("snare-ffmpeg-");
        try
        {
            var video = Path.Combine(folder.FullName, "test.mp4");
            await FfmpegRunner.RunAsync(tools.FfmpegPath!,
                ["-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc=duration=4:size=640x360:rate=30", "-pix_fmt", "yuv420p", "-g", "15", video],
                cancellationToken: ct);

            var info = await tools.ProbeAsync(video, ct);
            Assert.NotNull(info);
            Assert.Equal(new PixelSize(640, 360), info.Size);
            Assert.InRange(info.Duration.TotalSeconds, 3.9, 4.1);

            var thumb = await tools.ThumbnailPngAsync(video, info.Duration, ct);
            Assert.NotNull(thumb);
            Assert.Equal(0x89, thumb[0]); // PNG signature

            // Stream copy snaps to keyframes; the test video has one every 0.5 s.
            await tools.TrimInPlaceAsync(video, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), ct);
            var trimmed = await tools.ProbeAsync(video, ct);
            Assert.InRange(trimmed!.Duration.TotalSeconds, 1.5, 2.6);

            var gif = Path.Combine(folder.FullName, "test.gif");
            await tools.ExportGifAsync(video, gif, null, null, 640, ct);
            Assert.Equal("GIF8", System.Text.Encoding.ASCII.GetString((await File.ReadAllBytesAsync(gif, ct))[..4]));
            TestContext.Current.SendDiagnosticMessage($"probe {info}; trimmed {trimmed.Duration}; gif {new FileInfo(gif).Length:N0} bytes");
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    private sealed class StaticOptions : IOptionsMonitor<SnareOptions>
    {
        public SnareOptions CurrentValue { get; } = new();

        public SnareOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<SnareOptions, string?> listener) => null;
    }
}
