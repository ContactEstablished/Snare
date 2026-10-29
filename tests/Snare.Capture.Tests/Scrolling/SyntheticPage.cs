using Snare.Core.Imaging;

namespace Snare.Capture.Tests.Scrolling;

/// <summary>
/// A generated web page: lines of "words" (dark blocks of random widths), some colored image
/// blocks and blank gaps, seen through a viewport with a sticky header and footer.
/// </summary>
internal sealed class SyntheticPage
{
    private readonly PixelBuffer _page;

    public SyntheticPage(int width = 400, int pageHeight = 3000, int viewport = 300, int header = 50, int footer = 30, int seed = 7, int blankFrom = -1, int blankRows = 0)
    {
        Width = width;
        Viewport = viewport;
        Header = header;
        Footer = footer;
        _page = Generate(width, pageHeight, seed, blankFrom, blankRows);
        HeaderRows = Bar(width, header, 0xFF1E5AA8, seed + 1);
        FooterRows = Bar(width, footer, 0xFF333333, seed + 2);
    }

    public int Width { get; }

    public int Viewport { get; }

    public int Header { get; }

    public int Footer { get; }

    public PixelBuffer? HeaderRows { get; }

    public PixelBuffer? FooterRows { get; }

    /// <summary>The viewport scrolled to <paramref name="y"/>: page rows behind the header and footer, which stay put.</summary>
    public PixelBuffer Frame(int y, int noiseSeed = -1)
    {
        var stride = Width * 4;
        var pixels = new byte[stride * Viewport];
        HeaderRows?.Pixels.CopyTo(pixels, 0);
        _page.Pixels.AsSpan((y + Header) * stride, (Viewport - Header - Footer) * stride).CopyTo(pixels.AsSpan(Header * stride));
        FooterRows?.Pixels.CopyTo(pixels, (Viewport - Footer) * stride);
        if (noiseSeed >= 0)
        {
            var random = new Random(noiseSeed);
            for (var i = 0; i < pixels.Length; i++)
            {
                if (i % 4 != 3 && pixels[i] < 255 && random.Next(2) == 1)
                {
                    pixels[i]++;
                }
            }
        }

        return new PixelBuffer(Width, Viewport, pixels);
    }

    /// <summary>What a perfect stitch scrolled down to <paramref name="lastY"/> looks like: header once, the page, footer once.</summary>
    public PixelBuffer Expected(int lastY)
    {
        var stride = Width * 4;
        var height = lastY + Viewport;
        var pixels = new byte[stride * height];
        HeaderRows?.Pixels.CopyTo(pixels, 0);
        _page.Pixels.AsSpan(Header * stride, (lastY + Viewport - Header - Footer) * stride).CopyTo(pixels.AsSpan(Header * stride));
        FooterRows?.Pixels.CopyTo(pixels, (height - Footer) * stride);
        return new PixelBuffer(Width, height, pixels);
    }

    private static PixelBuffer Generate(int width, int height, int seed, int blankFrom, int blankRows)
    {
        var random = new Random(seed);
        var page = PixelBuffer.Filled(width, height, 0xFFFFFFFF);
        var y = 10;
        while (y < height - 30)
        {
            if (y >= blankFrom && y < blankFrom + blankRows)
            {
                y = blankFrom + blankRows;
                continue;
            }

            if (random.Next(9) == 0)
            {
                // An "image": a colored block with a gradient so it has texture.
                var h = random.Next(40, 90);
                var color = (uint)random.Next(0x202020, 0x909090);
                for (var row = 0; row < h && y + row < height; row++)
                {
                    Fill(page, 20, y + row, width - 80, 1, 0xFF000000 | (color + (uint)(row * 0x010101)));
                }

                y += h + 12;
                continue;
            }

            // A line of text: words 6-40 px wide, 10 px tall, gaps of 5 px.
            var x = 12 + random.Next(0, 20);
            while (x < width - 50)
            {
                var w = random.Next(6, 40);
                var shade = (uint)random.Next(0x10, 0x60);
                Fill(page, x, y, w, 10, 0xFF000000 | (shade << 16) | (shade << 8) | shade);
                x += w + 5;
            }

            y += 10 + random.Next(6, 14);
        }

        return page;
    }

    private static PixelBuffer? Bar(int width, int height, uint color, int seed)
    {
        if (height == 0)
        {
            return null;
        }

        var random = new Random(seed);
        var bar = PixelBuffer.Filled(width, height, color);
        for (var x = 10; x < width - 40; x += random.Next(30, 70))
        {
            Fill(bar, x, height / 3, random.Next(10, 25), Math.Max(1, height / 3), 0xFFFFFFFF);
        }

        return bar;
    }

    private static void Fill(PixelBuffer image, int x, int y, int w, int h, uint argb)
    {
        for (var row = y; row < y + h && row < image.Height; row++)
        {
            for (var col = x; col < x + w && col < image.Width; col++)
            {
                var i = (row * image.Stride) + (col * 4);
                image.Pixels[i] = (byte)argb;
                image.Pixels[i + 1] = (byte)(argb >> 8);
                image.Pixels[i + 2] = (byte)(argb >> 16);
                image.Pixels[i + 3] = (byte)(argb >> 24);
            }
        }
    }
}
