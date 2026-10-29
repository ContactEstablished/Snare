using SkiaSharp;
using Snare.Core.Annotations;

namespace Snare.Rendering;

/// <summary>
/// The one renderer for annotations: the editor preview calls <see cref="Draw"/> with its view
/// transform, and export calls <see cref="Render"/>. Both paint the same way, so the exported
/// PNG matches what Matt saw.
/// </summary>
public sealed class AnnotationRenderer
{
    private static readonly SKSamplingOptions ExportSampling = new(SKCubicResampler.Mitchell);

    /// <summary>
    /// Paints the original and every object in z-order, in original-image coordinates.
    /// The caller sets the canvas transform and clip. Crop and resize are not applied.
    /// </summary>
    public void Draw(SKCanvas canvas, SKImage original, AnnotationDocument document, SKSamplingOptions? sampling = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(document);

        canvas.DrawImage(original, 0, 0, sampling ?? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        DrawObjects(canvas, original, document);
    }

    /// <summary>Paints just the objects, in original-image coordinates.</summary>
    public void DrawObjects(SKCanvas canvas, SKImage original, AnnotationDocument document)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(document);

        // Blur and pixelate sample pixels. If they sampled the raw original, one drawn over (or
        // beside) a solid redaction would paint the secret back. They sample a copy with every
        // solid redaction already applied instead.
        using var redactedSource = HasSolidAndSampled(document) ? WithSolidRedactions(original, document) : null;
        var sampleSource = redactedSource ?? original;

        var step = 0;
        foreach (var item in document.Objects)
        {
            if (item is StepAnnotation)
            {
                step++;
            }

            AnnotationPainter.Paint(canvas, sampleSource, item, step);
        }
    }

    private static bool HasSolidAndSampled(AnnotationDocument document) =>
        document.Objects.OfType<RedactAnnotation>().Any(r => r.Mode == RedactMode.Solid)
        && document.Objects.OfType<RedactAnnotation>().Any(r => r.Mode != RedactMode.Solid);

    private static SKImage WithSolidRedactions(SKImage original, AnnotationDocument document)
    {
        using var surface = SKSurface.Create(new SKImageInfo(original.Width, original.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.DrawImage(original, 0, 0, SKSamplingOptions.Default);
        foreach (var solid in document.Objects.OfType<RedactAnnotation>().Where(r => r.Mode == RedactMode.Solid))
        {
            AnnotationPainter.Paint(surface.Canvas, original, solid, 0);
        }

        return surface.Snapshot();
    }

    /// <summary>The flattened export: original plus annotations, cropped, then resized.</summary>
    public SKBitmap Render(SKBitmap original, AnnotationDocument document)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(document);

        // Flatten at native size first, then scale the result. Scaling while drawing would let
        // resampling mix original pixels from under a solid redaction into its edges.
        var area = document.Canvas.ExportArea;
        var size = document.Canvas.OutputSize;
        var native = new SKBitmap(new SKImageInfo(area.Width, area.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var image = SKImage.FromBitmap(original))
        using (var canvas = new SKCanvas(native))
        {
            canvas.Clear(SKColors.Black);
            canvas.Translate(-area.X, -area.Y);
            canvas.ClipRect(new SKRect(area.X, area.Y, area.Right, area.Bottom));
            Draw(canvas, image, document, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
        }

        if (size.Width == area.Width && size.Height == area.Height)
        {
            return native;
        }

        using (native)
        {
            return native.Resize(new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul), ExportSampling)
                ?? throw new InvalidOperationException("SkiaSharp could not resize the export.");
        }
    }
}
