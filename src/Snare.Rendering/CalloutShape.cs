using SkiaSharp;
using Snare.Core.Annotations;
using Snare.Core.Geometry;

namespace Snare.Rendering;

/// <summary>
/// A callout's outline: the rounded bubble plus a tail that leaves from the side nearest the
/// tail point. Shared by painting and hit testing so both agree on the shape.
/// </summary>
public static class CalloutShape
{
    public static float CornerRadius(CalloutAnnotation callout) =>
        Math.Min(callout.FontSize * 0.6f, Math.Min(callout.Bounds.Width, callout.Bounds.Height) / 2f);

    /// <summary>The tail triangle (two base points on the bubble's edge and the tip), or null when the tip is inside the bubble.</summary>
    public static (SKPoint BaseA, SKPoint BaseB, SKPoint Tip)? Tail(CalloutAnnotation callout)
    {
        ArgumentNullException.ThrowIfNull(callout);
        var box = new SKRect(callout.Bounds.X, callout.Bounds.Y, callout.Bounds.Right, callout.Bounds.Bottom);
        var tip = new SKPoint(callout.Tail.X, callout.Tail.Y);
        if (box.Contains(tip) || box.Width < 2 || box.Height < 2)
        {
            return null;
        }

        var radius = CornerRadius(callout);
        var half = Math.Clamp(Math.Min(box.Width, box.Height) * 0.15f, 6, 30);
        var dx = (tip.X - box.MidX) / (box.Width / 2);
        var dy = (tip.Y - box.MidY) / (box.Height / 2);
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            var x = dx > 0 ? box.Right : box.Left;
            var y = Along(tip.Y, box.Top, box.Bottom, radius, half);
            return (new SKPoint(x, y - half), new SKPoint(x, y + half), tip);
        }
        else
        {
            var y = dy > 0 ? box.Bottom : box.Top;
            var x = Along(tip.X, box.Left, box.Right, radius, half);
            return (new SKPoint(x - half, y), new SKPoint(x + half, y), tip);
        }
    }

    /// <summary>The bubble and tail as one path, so the outline strokes once around both.</summary>
    public static SKPath Outline(CalloutAnnotation callout)
    {
        var box = new SKRect(callout.Bounds.X, callout.Bounds.Y, callout.Bounds.Right, callout.Bounds.Bottom);
        var radius = CornerRadius(callout);
        using var bubbleBuilder = new SKPathBuilder();
        bubbleBuilder.AddRoundRect(new SKRoundRect(box, radius, radius));
        var bubble = bubbleBuilder.Detach();
        if (Tail(callout) is not { } tail)
        {
            return bubble;
        }

        using var tailBuilder = new SKPathBuilder();
        // The base is pushed 2 px into the bubble so the union has no seam along the edge.
        var inward = new SKPoint(box.MidX, box.MidY) - new SKPoint((tail.BaseA.X + tail.BaseB.X) / 2, (tail.BaseA.Y + tail.BaseB.Y) / 2);
        var length = inward.Length;
        var nudge = length > 0 ? new SKPoint(inward.X / length * 2, inward.Y / length * 2) : SKPoint.Empty;
        tailBuilder.MoveTo(tail.BaseA + nudge);
        tailBuilder.LineTo(tail.Tip);
        tailBuilder.LineTo(tail.BaseB + nudge);
        tailBuilder.Close();
        using var tailPath = tailBuilder.Detach();
        if (bubble.Op(tailPath, SKPathOp.Union) is not { } union)
        {
            return bubble; // Skia couldn't combine them; the bubble alone is still right
        }

        bubble.Dispose();
        return union;
    }

    /// <summary>The callout's text as a text object at its padded top-left: painted, measured and edited like the Text tool's.</summary>
    public static TextAnnotation TextPart(CalloutAnnotation callout)
    {
        ArgumentNullException.ThrowIfNull(callout);
        var padding = (int)MathF.Round(callout.Padding);
        return new TextAnnotation(
            callout.Id, new PixelPoint(callout.Bounds.X + padding, callout.Bounds.Y + padding), callout.Text, callout.Color, callout.FontSize, Bold: callout.Bold);
    }

    /// <summary>Where on a side the tail's base centers: under the tip, but clear of the rounded corners.</summary>
    private static float Along(float tip, float start, float end, float radius, float half)
    {
        var low = start + radius + half;
        var high = end - radius - half;
        return low > high ? (start + end) / 2 : Math.Clamp(tip, low, high);
    }
}
