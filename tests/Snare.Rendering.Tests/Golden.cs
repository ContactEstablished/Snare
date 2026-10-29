using System.Runtime.CompilerServices;
using SkiaSharp;

namespace Snare.Rendering.Tests;

/// <summary>
/// Compares a render with a checked-in PNG in Golden/. A missing or mismatched golden writes
/// &lt;name&gt;.received.png next to the source (gitignored); review it and rename it to accept.
/// </summary>
internal static class Golden
{
    /// <summary>A channel may be off by this much before a pixel counts as different.</summary>
    private const int ChannelTolerance = 3;

    /// <summary>Up to this share of pixels may differ (antialiasing and font hinting).</summary>
    private const double PixelTolerance = 0.005;

    public static void AssertMatches(SKBitmap actual, string name, [CallerFilePath] string callerPath = "")
    {
        var expectedPath = Path.Combine(AppContext.BaseDirectory, "Golden", name + ".png");
        var sourceFolder = Path.Combine(Path.GetDirectoryName(callerPath) ?? "", "Golden");
        var receivedFolder = Directory.Exists(sourceFolder) ? sourceFolder : Path.Combine(AppContext.BaseDirectory, "Golden");
        var receivedPath = Path.Combine(receivedFolder, name + ".received.png");

        if (!File.Exists(expectedPath))
        {
            WriteReceived(actual, receivedPath);
            Assert.Fail($"No golden image for '{name}'. Wrote {receivedPath}; review it and rename it to {name}.png.");
        }

        using var expected = ImageCodec.Decode(File.ReadAllBytes(expectedPath));
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            WriteReceived(actual, receivedPath);
            Assert.Fail($"'{name}' is {actual.Width}x{actual.Height}; the golden is {expected.Width}x{expected.Height}. Wrote {receivedPath}.");
        }

        var different = 0;
        for (var y = 0; y < actual.Height; y++)
        {
            for (var x = 0; x < actual.Width; x++)
            {
                if (!Close(actual.GetPixel(x, y), expected.GetPixel(x, y)))
                {
                    different++;
                }
            }
        }

        var allowed = (int)(actual.Width * actual.Height * PixelTolerance);
        if (different > allowed)
        {
            WriteReceived(actual, receivedPath);
            Assert.Fail($"'{name}' differs from its golden in {different} pixels (allowed {allowed}). Wrote {receivedPath}.");
        }

        File.Delete(receivedPath);
    }

    private static bool Close(SKColor a, SKColor b) =>
        Math.Abs(a.Red - b.Red) <= ChannelTolerance
        && Math.Abs(a.Green - b.Green) <= ChannelTolerance
        && Math.Abs(a.Blue - b.Blue) <= ChannelTolerance
        && Math.Abs(a.Alpha - b.Alpha) <= ChannelTolerance;

    private static void WriteReceived(SKBitmap actual, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, ImageCodec.EncodePng(actual));
    }
}
