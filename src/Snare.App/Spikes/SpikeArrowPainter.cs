using SkiaSharp;

namespace Snare.App.Spikes;

/// <summary>Phase 0 stand-in for the arrow tool. The real one lives in Snare.Rendering in Phase 2.</summary>
internal static class SpikeArrowPainter
{
    public static void Draw(SKCanvas canvas, SKPoint from, SKPoint to, float strokeWidth)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(0xE5, 0x39, 0x35),
            IsAntialias = true,
            StrokeWidth = strokeWidth,
            StrokeCap = SKStrokeCap.Round,
            Style = SKPaintStyle.Stroke,
        };

        var direction = to - from;
        var length = direction.Length;
        if (length < 1)
        {
            return;
        }

        var unit = new SKPoint(direction.X / length, direction.Y / length);
        var normal = new SKPoint(-unit.Y, unit.X);
        var headLength = strokeWidth * 4;
        var headBase = to - new SKPoint(unit.X * headLength, unit.Y * headLength);

        canvas.DrawLine(from, headBase, paint);

        using var builder = new SKPathBuilder();
        builder.MoveTo(to);
        builder.LineTo(headBase + new SKPoint(normal.X * headLength * 0.6f, normal.Y * headLength * 0.6f));
        builder.LineTo(headBase - new SKPoint(normal.X * headLength * 0.6f, normal.Y * headLength * 0.6f));
        builder.Close();
        using var head = builder.Detach();
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawPath(head, paint);

        // Drag handles at both ends.
        using var handle = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawCircle(from, strokeWidth, handle);
        canvas.DrawCircle(to, strokeWidth, handle);
    }
}
