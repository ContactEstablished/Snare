using SkiaSharp;
using Snare.Core.Annotations;
using Snare.Core.Options;
using Snare.Rendering;

namespace Snare.Library;

/// <summary>Flattens an original plus annotations into export bytes. Used by the store and by Save As.</summary>
public static class ExportEncoder
{
    public static ExportFormat FormatOf(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" ? ExportFormat.Jpeg : ExportFormat.Png;

    public static byte[] Encode(SKBitmap flattened, ExportFormat format, int jpegQuality)
    {
        ArgumentNullException.ThrowIfNull(flattened);
        return format == ExportFormat.Jpeg ? ImageCodec.EncodeJpeg(flattened, jpegQuality) : ImageCodec.EncodePng(flattened);
    }

    /// <summary>Renders and encodes in one step.</summary>
    public static byte[] Render(AnnotationRenderer renderer, SKBitmap original, AnnotationDocument document, ExportFormat format, int jpegQuality)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        using var flattened = renderer.Render(original, document);
        return Encode(flattened, format, jpegQuality);
    }
}
