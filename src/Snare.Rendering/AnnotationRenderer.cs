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

        var step = 0;
        foreach (var item in document.Objects)
        {
            if (item is StepAnnotation)
            {
                step++;
            }

            AnnotationPainter.Paint(canvas, original, item, step);
        }
    }

    /// <summary>The flattened export: original plus annotations, cropped, then resized.</summary>
    public SKBitmap Render(SKBitmap original, AnnotationDocument document)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(document);

        var area = document.Canvas.ExportArea;
        var size = document.Canvas.OutputSize;
        var result = new SKBitmap(new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var image = SKImage.FromBitmap(original);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Black);
        canvas.Scale((float)size.Width / area.Width, (float)size.Height / area.Height);
        canvas.Translate(-area.X, -area.Y);
        canvas.ClipRect(new SKRect(area.X, area.Y, area.Right, area.Bottom));
        Draw(canvas, image, document, size.Width == area.Width && size.Height == area.Height
            ? new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None)
            : ExportSampling);
        return result;
    }
}
