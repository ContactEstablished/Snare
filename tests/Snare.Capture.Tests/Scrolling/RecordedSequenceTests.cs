using System.Text.Json;
using OpenCvSharp;
using Snare.Capture.Scrolling;
using Snare.Core.Imaging;

namespace Snare.Capture.Tests.Scrolling;

/// <summary>
/// Frame sequences recorded from a real browser (tools/scroll-fixture/record.mjs): real text
/// rendering, a sticky header, a fixed footer, a pause and a scroll back up, with the browser's
/// own scrollY for every frame.
/// </summary>
public class RecordedSequenceTests
{
    public static TheoryData<string> Sequences() => new(
        Directory.GetDirectories(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Scrolling")).Select(Path.GetFileName)!);

    [Theory]
    [MemberData(nameof(Sequences))]
    public void StitchesTheRecordedSequence_MatchingTheBrowsersScrollPositions(string sequence)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Scrolling", sequence);
        var info = JsonSerializer.Deserialize<FixtureInfo>(File.ReadAllText(Path.Combine(folder, "frames.json")), Json)!;
        var frames = info.Frames.Select(f => Load(Path.Combine(folder, f.File))).ToList();
        var stitcher = new ScrollStitcher();

        var reference = info.Frames[0].ScrollY;
        for (var i = 0; i < frames.Count; i++)
        {
            var result = stitcher.Add(frames[i]);
            var y = info.Frames[i].ScrollY;
            var expected = i == 0 ? StitchOutcome.Started
                : y == reference ? StitchOutcome.NoMovement
                : y < reference ? StitchOutcome.ScrolledBack
                : StitchOutcome.Appended;
            Assert.True(expected == result.Outcome, $"Frame {i} (scrollY {y}, reference {reference}): expected {expected}, got {result.Outcome} {result.Warning}");
            if (result.Outcome == StitchOutcome.Appended)
            {
                Assert.Equal(y - reference, result.Offset);
                reference = y;
            }
        }

        var image = stitcher.Compose();
        Assert.Equal(reference + info.Height, image.Height);

        // Every frame's scrolling content sits at its scrollY; header and footer appear once, at the ends.
        var header = (int)Math.Ceiling(info.Header);
        var footer = (int)Math.Ceiling(info.Footer);
        for (var i = 0; i < frames.Count; i++)
        {
            var y = info.Frames[i].ScrollY;
            AssertRowsEqual(frames[i], header, image, y + header, info.Height - header - footer, $"frame {i}");
        }

        AssertRowsEqual(frames[0], 0, image, 0, header, "header");
        AssertRowsEqual(frames[^1], info.Height - footer, image, image.Height - footer, footer, "footer");
    }

    private static void AssertRowsEqual(PixelBuffer a, int aTop, PixelBuffer b, int bTop, int rows, string what)
    {
        var stride = a.Stride;
        var left = a.Pixels.AsSpan(aTop * stride, rows * stride);
        var right = b.Pixels.AsSpan(bTop * stride, rows * stride);
        var differing = 0;
        for (var i = 0; i < left.Length; i++)
        {
            if (Math.Abs(left[i] - right[i]) > 2)
            {
                differing++;
            }
        }

        Assert.True(differing == 0, $"{what}: {differing} bytes differ from the stitched image at row {bTop}.");
    }

    private static PixelBuffer Load(string path)
    {
        using var bgr = Cv2.ImRead(path, ImreadModes.Color);
        using var bgra = new Mat();
        Cv2.CvtColor(bgr, bgra, ColorConversionCodes.BGR2BGRA);
        var pixels = new byte[bgra.Rows * bgra.Cols * 4];
        System.Runtime.InteropServices.Marshal.Copy(bgra.Data, pixels, 0, pixels.Length);
        return new PixelBuffer(bgra.Cols, bgra.Rows, pixels);
    }

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed record FixtureInfo(int Width, int Height, double Header, double Footer, IReadOnlyList<FixtureFrame> Frames);

    private sealed record FixtureFrame(string File, int ScrollY);
}
