using System.Runtime.InteropServices.WindowsRuntime;
using Snare.Core.Geometry;
using Snare.Core.Imaging;
using Snare.Core.Search;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Snare.Library.Ocr;

/// <summary>Windows' built-in, offline OCR in the user-profile language (English on Matt's PC).</summary>
public sealed class WindowsOcrEngine : IOcrEngine
{
    private readonly Lazy<OcrEngine?> _engine = new(OcrEngine.TryCreateFromUserProfileLanguages);

    public async Task<string> RecognizeAsync(PixelBuffer image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        var engine = _engine.Value
            ?? throw new InvalidOperationException("Windows has no OCR language installed for the user profile.");

        var tiles = OcrTiling.Plan(image.Width, image.Height, (int)OcrEngine.MaxImageDimension);
        var lines = new List<IReadOnlyList<string>>();
        foreach (var tile in tiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var bitmap = ToSoftwareBitmap(image, tile);
            var result = await engine.RecognizeAsync(bitmap).AsTask(cancellationToken);
            lines.Add(result.Lines.Select(l => l.Text).ToList());
        }

        return OcrTiling.JoinLines(lines);
    }

    private static SoftwareBitmap ToSoftwareBitmap(PixelBuffer image, PixelRect tile)
    {
        var bytes = new byte[PixelBuffer.ByteCount(tile.Width, tile.Height)];
        var rowBytes = tile.Width * PixelBuffer.BytesPerPixel;
        for (var row = 0; row < tile.Height; row++)
        {
            Buffer.BlockCopy(image.Pixels, ((tile.Y + row) * image.Stride) + (tile.X * PixelBuffer.BytesPerPixel), bytes, row * rowBytes, rowBytes);
        }

        return SoftwareBitmap.CreateCopyFromBuffer(bytes.AsBuffer(), BitmapPixelFormat.Bgra8, tile.Width, tile.Height, BitmapAlphaMode.Premultiplied);
    }
}
