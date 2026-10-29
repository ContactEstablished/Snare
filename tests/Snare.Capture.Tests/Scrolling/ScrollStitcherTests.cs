using Snare.Capture.Scrolling;
using Snare.Core.Imaging;

namespace Snare.Capture.Tests.Scrolling;

public class ScrollStitcherTests
{
    [Fact]
    public void StitchesAPageWithAStickyHeaderAndFooter_Exactly()
    {
        var page = new SyntheticPage();
        var stitcher = new ScrollStitcher();
        int[] positions = [0, 40, 125, 245, 395, 500, 640, 780];

        var outcomes = positions.Select(y => stitcher.Add(page.Frame(y))).ToList();

        Assert.Equal(StitchOutcome.Started, outcomes[0].Outcome);
        Assert.All(outcomes.Skip(1), r => Assert.Equal(StitchOutcome.Appended, r.Outcome));
        Assert.Equal([40, 85, 120, 150, 105, 140, 140], outcomes.Skip(1).Select(r => r.Offset));
        AssertSame(page.Expected(780), stitcher.Compose());
    }

    [Fact]
    public void IgnoresStillFramesAndScrollingBack()
    {
        var page = new SyntheticPage();
        var stitcher = new ScrollStitcher();

        stitcher.Add(page.Frame(0));
        Assert.Equal(StitchOutcome.Appended, stitcher.Add(page.Frame(100)).Outcome);
        Assert.Equal(StitchOutcome.NoMovement, stitcher.Add(page.Frame(100)).Outcome);
        var back = stitcher.Add(page.Frame(60));
        Assert.Equal(StitchOutcome.ScrolledBack, back.Outcome);
        Assert.NotNull(back.Warning);
        Assert.Equal(StitchOutcome.NoMovement, stitcher.Add(page.Frame(100)).Outcome);
        Assert.Equal(StitchOutcome.Appended, stitcher.Add(page.Frame(200)).Outcome);

        AssertSame(page.Expected(200), stitcher.Compose());
    }

    [Fact]
    public void DropsAJumpTooBigToLineUp_AndRecoversWhenTheNextFrameOverlaps()
    {
        var page = new SyntheticPage();
        var stitcher = new ScrollStitcher();
        stitcher.Add(page.Frame(0));

        var jump = stitcher.Add(page.Frame(600)); // far more than the 220-row content band
        Assert.Equal(StitchOutcome.NoMatch, jump.Outcome);
        Assert.NotNull(jump.Warning);

        Assert.Equal(StitchOutcome.Appended, stitcher.Add(page.Frame(150)).Outcome);
        AssertSame(page.Expected(150), stitcher.Compose());
    }

    [Fact]
    public void AGradientNeverPassesForAScrollBack()
    {
        // Frame 900 shares no rows with frame 0, but both hold gradient "images", which
        // normalized correlation alone scores as a near-perfect match.
        var page = new SyntheticPage();
        var stitcher = new ScrollStitcher();
        stitcher.Add(page.Frame(0));

        Assert.Equal(StitchOutcome.NoMatch, stitcher.Add(page.Frame(900)).Outcome);
    }

    [Fact]
    public void LinesUpExactlyWhereTheStripFallsInAGradient()
    {
        // At 900 -> 1050 the lowest strip sits in a gradient block, which matches one row off.
        var page = new SyntheticPage(pageHeight: 1400);
        var stitcher = new ScrollStitcher();
        foreach (var y in new[] { 0, 150, 300, 450, 600, 750, 900, 1050, 1100 })
        {
            stitcher.Add(page.Frame(y));
        }

        AssertSame(page.Expected(1100), stitcher.Compose());
    }

    [Fact]
    public void IgnoresStaticSideColumns_LikeADocsTableOfContents()
    {
        // 600 wide: a fixed 150 px table of contents on the left and a 100 px column on the
        // right, beside content that scrolls. Both columns have text, so they're worth matching.
        var page = new SyntheticPage(width: 600, seed: 21);
        var stitcher = new ScrollStitcher();
        int[] positions = [0, 120, 240, 390, 520, 660];

        var outcomes = positions.Select(y => stitcher.Add(Decorate(page.Frame(y), page, left: 150, right: 100))).ToList();

        Assert.All(outcomes.Skip(1), r => Assert.Equal(StitchOutcome.Appended, r.Outcome));
        Assert.Equal([120, 120, 150, 130, 140], outcomes.Skip(1).Select(r => r.Offset));
        AssertSameColumns(page.Expected(660), stitcher.Compose(), 150, 500);
    }

    [Fact]
    public void ToleratesAMovingScrollbarThumbBesideANarrowArea()
    {
        var page = new SyntheticPage(width: 400, seed: 5);
        var stitcher = new ScrollStitcher();
        int[] positions = [0, 100, 200, 300, 420, 540];

        foreach (var y in positions)
        {
            var result = stitcher.Add(Thumb(page.Frame(y), page, y));
            Assert.NotEqual(StitchOutcome.NoMatch, result.Outcome);
            Assert.NotEqual(StitchOutcome.NoMovement, result.Outcome);
        }

        AssertSameColumns(page.Expected(540), stitcher.Compose(), 0, 388);
    }

    [Fact]
    public void ABlinkingElementInAStaticFooterDoesntStopTheMatch()
    {
        var page = new SyntheticPage(seed: 9);
        var stitcher = new ScrollStitcher();
        int[] positions = [0, 90, 180, 270, 360, 450];

        for (var i = 0; i < positions.Length; i++)
        {
            var frame = Blink(page.Frame(positions[i]), page, on: i % 2 == 0);
            Assert.Equal(i == 0 ? StitchOutcome.Started : StitchOutcome.Appended, stitcher.Add(frame).Outcome);
        }

        // Everything above the footer matches the page; the footer is the last frame's.
        var image = stitcher.Compose();
        var expected = page.Expected(450);
        AssertSame(Crop(expected, 0, expected.Height - page.Footer), Crop(image, 0, image.Height - page.Footer));
    }

    [Fact]
    public void ABlinkingElementAloneIsNoMovement()
    {
        var page = new SyntheticPage(seed: 9);
        var stitcher = new ScrollStitcher();
        stitcher.Add(Blink(page.Frame(100), page, on: true));

        Assert.Equal(StitchOutcome.NoMovement, stitcher.Add(Blink(page.Frame(100), page, on: false)).Outcome);
    }

    /// <summary>Static text columns over the frame's left and right edges, below the header and above the footer.</summary>
    private static PixelBuffer Decorate(PixelBuffer frame, SyntheticPage page, int left, int right)
    {
        var random = new Random(99); // same pattern in every frame
        for (var y = page.Header; y < frame.Height - page.Footer; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                if (x < left || x >= frame.Width - right)
                {
                    Set(frame, x, y, 0xFFF4F4F4);
                }
            }
        }

        for (var y = page.Header + 8; y < frame.Height - page.Footer - 12; y += 18)
        {
            foreach (var (from, to) in new[] { (10, left - 10), (frame.Width - right + 8, frame.Width - 8) })
            {
                var w = random.Next(30, to - from);
                for (var row = y; row < y + 9; row++)
                {
                    for (var x = from; x < from + w; x++)
                    {
                        Set(frame, x, row, 0xFF404040);
                    }
                }
            }
        }

        return frame;
    }

    /// <summary>A 12 px scrollbar on the right whose 40 px thumb moves with the scroll position, over the header too.</summary>
    private static PixelBuffer Thumb(PixelBuffer frame, SyntheticPage page, int y)
    {
        var thumbTop = y * (frame.Height - 40) / 2700;
        for (var row = 0; row < frame.Height; row++)
        {
            for (var x = frame.Width - 12; x < frame.Width; x++)
            {
                Set(frame, x, row, row >= thumbTop && row < thumbTop + 40 ? 0xFF8A8A8A : 0xFFF0F0F0);
            }
        }

        return frame;
    }

    /// <summary>A 16x16 status light in the footer, on or off.</summary>
    private static PixelBuffer Blink(PixelBuffer frame, SyntheticPage page, bool on)
    {
        var top = frame.Height - page.Footer + ((page.Footer - 16) / 2);
        for (var row = top; row < top + 16; row++)
        {
            for (var x = 200; x < 216; x++)
            {
                Set(frame, x, row, on ? 0xFF43A047 : 0xFF333333);
            }
        }

        return frame;
    }

    private static void Set(PixelBuffer image, int x, int y, uint argb)
    {
        var i = (y * image.Stride) + (x * 4);
        image.Pixels[i] = (byte)argb;
        image.Pixels[i + 1] = (byte)(argb >> 8);
        image.Pixels[i + 2] = (byte)(argb >> 16);
        image.Pixels[i + 3] = (byte)(argb >> 24);
    }

    private static void AssertSameColumns(PixelBuffer expected, PixelBuffer actual, int left, int right)
    {
        Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = left; x < right; x++)
            {
                if (expected.GetArgb(x, y) != actual.GetArgb(x, y))
                {
                    Assert.Fail($"First difference at row {y}, column {x}.");
                }
            }
        }
    }

    [Fact]
    public void ToleratesGrabNoise()
    {
        var page = new SyntheticPage(seed: 11);
        var stitcher = new ScrollStitcher();
        int[] positions = [0, 70, 160, 290, 400];

        for (var i = 0; i < positions.Length; i++)
        {
            Assert.NotEqual(StitchOutcome.NoMatch, stitcher.Add(page.Frame(positions[i], noiseSeed: i)).Outcome);
        }

        AssertSame(page.Expected(400), stitcher.Compose(), tolerance: 2);
    }

    [Fact]
    public void LinesUpAcrossABlankGapSmallerThanTheView()
    {
        var page = new SyntheticPage(blankFrom: 400, blankRows: 150);
        var stitcher = new ScrollStitcher();
        int[] positions = [0, 100, 200, 300, 400, 500, 600];

        foreach (var y in positions)
        {
            Assert.Equal(y == 0 ? StitchOutcome.Started : StitchOutcome.Appended, stitcher.Add(page.Frame(y)).Outcome);
        }

        AssertSame(page.Expected(600), stitcher.Compose());
    }

    [Fact]
    public void WorksWithoutStickyRows()
    {
        var page = new SyntheticPage(header: 0, footer: 0);
        var stitcher = new ScrollStitcher();
        foreach (var y in new[] { 0, 90, 180, 330 })
        {
            stitcher.Add(page.Frame(y));
        }

        AssertSame(page.Expected(330), stitcher.Compose());
    }

    [Fact]
    public void StopsAtTheHeightCap()
    {
        var page = new SyntheticPage();
        var stitcher = new ScrollStitcher(maxHeight: 700);
        var outcomes = new[] { 0, 150, 300, 450, 600 }.Select(y => stitcher.Add(page.Frame(y)).Outcome).ToList();

        Assert.Equal(StitchOutcome.LimitReached, outcomes[3]); // 450 + 300 rows reaches 700
        Assert.Equal(StitchOutcome.LimitReached, outcomes[4]);
        Assert.True(stitcher.IsFull);
        var image = stitcher.Compose();
        Assert.Equal(700, image.Height);
        AssertSame(Crop(page.Expected(450), 0, 700), image); // the footer is cut off by the cap
    }

    [Fact]
    public void TailIsTheBottomOfTheImage()
    {
        var page = new SyntheticPage();
        var stitcher = new ScrollStitcher();
        foreach (var y in new[] { 0, 120, 240, 360 })
        {
            stitcher.Add(page.Frame(y));
        }

        var image = stitcher.Compose();
        AssertSame(Crop(image, image.Height - 500, 500), stitcher.Tail(500));
        Assert.Equal(image.Height, stitcher.Tail(100_000).Height);
        Assert.Equal(image.Height, stitcher.Height);
    }

    [Fact]
    public void RejectsAFrameOfADifferentSize()
    {
        var stitcher = new ScrollStitcher();
        stitcher.Add(PixelBuffer.Filled(100, 100, 0xFFFFFFFF));

        Assert.Throws<ArgumentException>(() => stitcher.Add(PixelBuffer.Filled(100, 90, 0xFFFFFFFF)));
    }

    [Fact]
    public void ABlankPageNeverAppends()
    {
        var stitcher = new ScrollStitcher();
        stitcher.Add(PixelBuffer.Filled(200, 200, 0xFFFFFFFF));

        Assert.Equal(StitchOutcome.NoMovement, stitcher.Add(PixelBuffer.Filled(200, 200, 0xFFFFFFFF)).Outcome);
        Assert.Equal(200, stitcher.Compose().Height);
    }

    internal static PixelBuffer Crop(PixelBuffer image, int top, int rows)
    {
        var pixels = image.Pixels.AsSpan(top * image.Stride, rows * image.Stride).ToArray();
        return new PixelBuffer(image.Width, rows, pixels);
    }

    internal static void AssertSame(PixelBuffer expected, PixelBuffer actual, int tolerance = 0)
    {
        Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
        for (var i = 0; i < expected.Pixels.Length; i++)
        {
            if (Math.Abs(expected.Pixels[i] - actual.Pixels[i]) > tolerance)
            {
                var row = i / expected.Stride;
                Assert.Fail($"First difference at row {row}, column {i % expected.Stride / 4}: {expected.Pixels[i]} vs {actual.Pixels[i]}.");
            }
        }
    }
}
