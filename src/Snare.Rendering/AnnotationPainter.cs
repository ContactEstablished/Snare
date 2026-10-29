using System.Globalization;
using SkiaSharp;
using Snare.Core.Annotations;
using Snare.Core.Geometry;

namespace Snare.Rendering;

/// <summary>Draws single annotation objects in original-image coordinates.</summary>
internal static class AnnotationPainter
{
    public const float HighlighterOpacity = 0.4f;

    public static void Paint(SKCanvas canvas, SKImage original, AnnotationObject item, int stepNumber)
    {
        switch (item)
        {
            case ArrowAnnotation arrow:
                PaintArrow(canvas, arrow);
                break;
            case LineAnnotation line:
                PaintLine(canvas, line);
                break;
            case RectangleAnnotation rect:
                PaintRectangle(canvas, rect);
                break;
            case EllipseAnnotation ellipse:
                PaintEllipse(canvas, ellipse);
                break;
            case PenAnnotation pen:
                PaintStroke(canvas, pen.Points, Stroke(pen.Stroke, pen.StrokeWidth));
                break;
            case HighlighterAnnotation highlighter:
                PaintHighlighter(canvas, highlighter);
                break;
            case TextAnnotation text:
                PaintText(canvas, text);
                break;
            case RedactAnnotation redact:
                PaintRedact(canvas, original, redact);
                break;
            case StepAnnotation step:
                PaintStep(canvas, step, stepNumber);
                break;
        }
    }

    private static SKPaint Stroke(string color, float width) => new()
    {
        Color = SkiaColors.Parse(color, SKColors.Red),
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = Math.Max(0.5f, width),
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    private static SKPaint Fill(string color) => new()
    {
        Color = SkiaColors.Parse(color, SKColors.Black),
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
    };

    private static SKPoint P(PixelPoint p) => new(p.X, p.Y);

    private static SKRect R(PixelRect r) => new(r.X, r.Y, r.Right, r.Bottom);

    private static void PaintArrow(SKCanvas canvas, ArrowAnnotation arrow)
    {
        using var paint = Stroke(arrow.Stroke, arrow.StrokeWidth);
        var from = P(arrow.From);
        var to = P(arrow.To);
        var direction = to - from;
        var length = direction.Length;
        if (length < 1)
        {
            return;
        }

        var unit = new SKPoint(direction.X / length, direction.Y / length);
        var normal = new SKPoint(-unit.Y, unit.X);
        var headLength = Math.Min(arrow.StrokeWidth * arrow.HeadSize, length * 0.6f);
        var halfWidth = headLength * 0.55f;
        var headBase = to - Scale(unit, headLength);
        var left = headBase + Scale(normal, halfWidth);
        var right = headBase - Scale(normal, halfWidth);

        if (arrow.HeadStyle == ArrowHeadStyle.Open)
        {
            canvas.DrawLine(from, to, paint);
            canvas.DrawLine(left, to, paint);
            canvas.DrawLine(right, to, paint);
            return;
        }

        // The shaft stops inside the head so its round cap doesn't poke out of the tip.
        canvas.DrawLine(from, headBase + Scale(unit, headLength * 0.3f), paint);
        using var builder = new SKPathBuilder();
        builder.MoveTo(to);
        builder.LineTo(left);
        builder.LineTo(right);
        builder.Close();
        using var head = builder.Detach();
        paint.Style = SKPaintStyle.StrokeAndFill;
        paint.StrokeWidth = Math.Max(1, arrow.StrokeWidth * 0.5f);
        canvas.DrawPath(head, paint);
    }

    private static void PaintLine(SKCanvas canvas, LineAnnotation line)
    {
        using var paint = Stroke(line.Stroke, line.StrokeWidth);
        if (line.Dashed)
        {
            paint.StrokeCap = SKStrokeCap.Butt;
            paint.PathEffect = SKPathEffect.CreateDash([line.StrokeWidth * 3, line.StrokeWidth * 2], 0);
        }

        canvas.DrawLine(P(line.From), P(line.To), paint);
    }

    private static void PaintRectangle(SKCanvas canvas, RectangleAnnotation rect)
    {
        var bounds = R(rect.Bounds);
        if (rect.Fill is { } fill)
        {
            using var fillPaint = Fill(fill);
            canvas.DrawRoundRect(bounds, rect.CornerRadius, rect.CornerRadius, fillPaint);
        }

        if (rect.StrokeWidth > 0)
        {
            using var stroke = Stroke(rect.Stroke, rect.StrokeWidth);
            canvas.DrawRoundRect(bounds, rect.CornerRadius, rect.CornerRadius, stroke);
        }
    }

    private static void PaintEllipse(SKCanvas canvas, EllipseAnnotation ellipse)
    {
        var bounds = R(ellipse.Bounds);
        if (ellipse.Fill is { } fill)
        {
            using var fillPaint = Fill(fill);
            canvas.DrawOval(bounds, fillPaint);
        }

        if (ellipse.StrokeWidth > 0)
        {
            using var stroke = Stroke(ellipse.Stroke, ellipse.StrokeWidth);
            canvas.DrawOval(bounds, stroke);
        }
    }

    private static void PaintHighlighter(SKCanvas canvas, HighlighterAnnotation highlighter)
    {
        var color = SkiaColors.Parse(highlighter.Color, SKColors.Yellow);
        using var paint = Stroke(highlighter.Color, highlighter.Width);
        paint.Color = color.WithAlpha((byte)(255 * HighlighterOpacity));
        paint.BlendMode = SKBlendMode.Multiply;
        paint.StrokeCap = SKStrokeCap.Square;

        // One path, so overlapping parts of the stroke don't darken twice.
        PaintStroke(canvas, highlighter.Points, paint);
    }

    /// <summary>A smoothed path through freehand points: quadratic curves via the midpoints.</summary>
    internal static SKPath SmoothPath(IReadOnlyList<PixelPoint> points)
    {
        using var builder = new SKPathBuilder();
        if (points.Count == 0)
        {
            return builder.Detach();
        }

        builder.MoveTo(P(points[0]));
        if (points.Count == 1)
        {
            builder.LineTo(P(points[0]));
        }

        for (var i = 1; i < points.Count - 1; i++)
        {
            var current = P(points[i]);
            var next = P(points[i + 1]);
            builder.QuadTo(current, new SKPoint((current.X + next.X) / 2, (current.Y + next.Y) / 2));
        }

        if (points.Count > 1)
        {
            builder.LineTo(P(points[^1]));
        }

        return builder.Detach();
    }

    private static void PaintStroke(SKCanvas canvas, IReadOnlyList<PixelPoint> points, SKPaint paint)
    {
        using (paint)
        {
            if (points.Count == 1)
            {
                using var dot = paint.Clone();
                dot.Style = SKPaintStyle.Fill;
                canvas.DrawCircle(P(points[0]), paint.StrokeWidth / 2, dot);
                return;
            }

            using var path = SmoothPath(points);
            canvas.DrawPath(path, paint);
        }
    }

    private static void PaintText(SKCanvas canvas, TextAnnotation text)
    {
        using var layout = TextLayout.For(text);
        if (text.Background is { } background)
        {
            using var fill = Fill(background);
            var radius = text.FontSize * 0.2f;
            canvas.DrawRoundRect(layout.BoundsAt(text.Position.X, text.Position.Y), radius, radius, fill);
        }

        using var paint = Fill(text.Color);
        for (var i = 0; i < layout.Lines.Count; i++)
        {
            canvas.DrawText(
                layout.Lines[i], text.Position.X, text.Position.Y + layout.Ascent + (i * layout.LineHeight),
                SKTextAlign.Left, layout.Font, paint);
        }
    }

    private static void PaintRedact(SKCanvas canvas, SKImage original, RedactAnnotation redact)
    {
        var bounds = R(redact.Bounds);
        switch (redact.Mode)
        {
            case RedactMode.Solid:
                // Opaque and not antialiased: every covered pixel becomes exactly the fill color.
                using (var paint = new SKPaint
                {
                    Color = SkiaColors.Parse(redact.Fill, SKColors.Black).WithAlpha(0xFF),
                    IsAntialias = false,
                    Style = SKPaintStyle.Fill,
                })
                {
                    canvas.DrawRect(bounds, paint);
                }

                break;
            case RedactMode.Blur:
                PaintBlur(canvas, original, bounds, redact.EffectiveStrength);
                break;
            case RedactMode.Pixelate:
                PaintPixelate(canvas, original, redact.Bounds, (int)Math.Max(2, redact.EffectiveStrength));
                break;
        }
    }

    /// <summary>Blurs the original pixels in the bounds (not annotations beneath).</summary>
    private static void PaintBlur(SKCanvas canvas, SKImage original, SKRect bounds, float sigma)
    {
        var margin = sigma * 3;
        var source = SKRect.Intersect(SKRect.Inflate(bounds, margin, margin), new SKRect(0, 0, original.Width, original.Height));
        if (source.IsEmpty)
        {
            return;
        }

        using var filter = SKImageFilter.CreateBlur(sigma, sigma, SKShaderTileMode.Clamp);
        using var paint = new SKPaint { ImageFilter = filter };
        canvas.Save();
        canvas.ClipRect(bounds, antialias: false);
        canvas.DrawImage(original, source, source, SKSamplingOptions.Default, paint);
        canvas.Restore();
    }

    /// <summary>Averages the original into blocks aligned to the bounds' top-left, then scales up without smoothing.</summary>
    private static void PaintPixelate(SKCanvas canvas, SKImage original, PixelRect bounds, int block)
    {
        var area = bounds.Intersect(new PixelRect(0, 0, original.Width, original.Height));
        if (area.IsEmpty)
        {
            return;
        }

        var smallWidth = (area.Width + block - 1) / block;
        var smallHeight = (area.Height + block - 1) / block;
        using var subset = original.Subset(new SKRectI(area.X, area.Y, area.Right, area.Bottom));
        if (subset is null)
        {
            return;
        }

        using var small = new SKBitmap(new SKImageInfo(smallWidth, smallHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var pixmap = small.PeekPixels())
        {
            subset.ScalePixels(pixmap, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }

        using var smallImage = SKImage.FromBitmap(small);
        var destination = new SKRect(area.X, area.Y, area.X + (smallWidth * block), area.Y + (smallHeight * block));
        canvas.Save();
        canvas.ClipRect(R(area), antialias: false);
        canvas.DrawImage(smallImage, destination, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
        canvas.Restore();
    }

    private static void PaintStep(SKCanvas canvas, StepAnnotation step, int number)
    {
        var center = P(step.Center);
        using var fill = Fill(step.Fill);
        canvas.DrawCircle(center, step.Radius, fill);

        var label = number.ToString(CultureInfo.InvariantCulture);
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold) ?? SKTypeface.Default, step.Radius * (label.Length > 1 ? 0.95f : 1.15f));
        using var text = new SKPaint { Color = SKColors.White, IsAntialias = true };
        var metrics = font.Metrics;
        var baseline = center.Y - ((metrics.Ascent + metrics.Descent) / 2);
        canvas.DrawText(label, center.X, baseline, SKTextAlign.Center, font, text);
    }

    private static SKPoint Scale(SKPoint p, float s) => new(p.X * s, p.Y * s);
}
