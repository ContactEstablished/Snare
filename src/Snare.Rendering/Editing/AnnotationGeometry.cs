using SkiaSharp;
using Snare.Core.Annotations;
using Snare.Core.Geometry;

namespace Snare.Rendering.Editing;

/// <summary>Bounds, hit testing, moving and resizing for annotation objects, in original-image pixels.</summary>
public static class AnnotationGeometry
{
    /// <summary>The object's visual bounds, including stroke width.</summary>
    public static SKRect Bounds(AnnotationObject item)
    {
        ArgumentNullException.ThrowIfNull(item);
        switch (item)
        {
            case ArrowAnnotation a:
                return Inflate(Around(a.From, a.To), a.StrokeWidth * Math.Max(1, a.HeadSize) * 0.6f);
            case LineAnnotation l:
                return Inflate(Around(l.From, l.To), l.StrokeWidth / 2);
            case RectangleAnnotation r:
                return Inflate(R(r.Bounds), r.StrokeWidth / 2);
            case EllipseAnnotation e:
                return Inflate(R(e.Bounds), e.StrokeWidth / 2);
            case PenAnnotation p:
                return Inflate(Around(p.Points), p.StrokeWidth / 2);
            case HighlighterAnnotation h:
                return Inflate(Around(h.Points), h.Width / 2);
            case TextAnnotation t:
                using (var layout = TextLayout.For(t))
                {
                    return layout.BoundsAt(t.Position.X, t.Position.Y);
                }

            case RedactAnnotation x:
                return R(x.Bounds);
            case StepAnnotation s:
                return new SKRect(s.Center.X - s.Radius, s.Center.Y - s.Radius, s.Center.X + s.Radius, s.Center.Y + s.Radius);
            default:
                return SKRect.Empty;
        }
    }

    /// <summary>True when the point is on the object, within a tolerance in image pixels.</summary>
    public static bool HitTest(AnnotationObject item, SKPoint p, float tolerance)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item switch
        {
            ArrowAnnotation a => DistanceToSegment(p, P(a.From), P(a.To)) <= (a.StrokeWidth * 1.5f) + tolerance,
            LineAnnotation l => DistanceToSegment(p, P(l.From), P(l.To)) <= (l.StrokeWidth / 2) + tolerance,
            RectangleAnnotation r => HitBox(R(r.Bounds), p, r.Fill is not null, r.StrokeWidth, tolerance),
            EllipseAnnotation e => HitEllipse(R(e.Bounds), p, e.Fill is not null, e.StrokeWidth, tolerance),
            PenAnnotation pen => DistanceToPolyline(p, pen.Points) <= (pen.StrokeWidth / 2) + tolerance,
            HighlighterAnnotation h => DistanceToPolyline(p, h.Points) <= (h.Width / 2) + tolerance,
            TextAnnotation or RedactAnnotation => Inflate(Bounds(item), tolerance).Contains(p),
            StepAnnotation s => SKPoint.Distance(p, P(s.Center)) <= s.Radius + tolerance,
            _ => false,
        };
    }

    /// <summary>A copy moved by whole pixels.</summary>
    public static AnnotationObject Translate(AnnotationObject item, int dx, int dy)
    {
        ArgumentNullException.ThrowIfNull(item);
        PixelPoint Move(PixelPoint p) => new(p.X + dx, p.Y + dy);
        PixelRect MoveRect(PixelRect r) => r with { X = r.X + dx, Y = r.Y + dy };
        return item switch
        {
            ArrowAnnotation a => a with { From = Move(a.From), To = Move(a.To) },
            LineAnnotation l => l with { From = Move(l.From), To = Move(l.To) },
            RectangleAnnotation r => r with { Bounds = MoveRect(r.Bounds) },
            EllipseAnnotation e => e with { Bounds = MoveRect(e.Bounds) },
            PenAnnotation pen => pen with { Points = [.. pen.Points.Select(Move)] },
            HighlighterAnnotation h => h with { Points = [.. h.Points.Select(Move)] },
            TextAnnotation t => t with { Position = Move(t.Position) },
            RedactAnnotation x => x with { Bounds = MoveRect(x.Bounds) },
            StepAnnotation s => s with { Center = Move(s.Center) },
            _ => item,
        };
    }

    /// <summary>Box-like objects (rectangle, ellipse, redact) can be resized with corner and edge handles.</summary>
    public static PixelRect? BoxOf(AnnotationObject item) => item switch
    {
        RectangleAnnotation r => r.Bounds,
        EllipseAnnotation e => e.Bounds,
        RedactAnnotation x => x.Bounds,
        _ => null,
    };

    public static AnnotationObject WithBox(AnnotationObject item, PixelRect box) => item switch
    {
        RectangleAnnotation r => r with { Bounds = box },
        EllipseAnnotation e => e with { Bounds = box },
        RedactAnnotation x => x with { Bounds = box },
        _ => item,
    };

    /// <summary>Lines and arrows have endpoint handles instead of a box.</summary>
    public static (PixelPoint From, PixelPoint To)? EndpointsOf(AnnotationObject item) => item switch
    {
        ArrowAnnotation a => (a.From, a.To),
        LineAnnotation l => (l.From, l.To),
        _ => null,
    };

    public static AnnotationObject WithEndpoints(AnnotationObject item, PixelPoint from, PixelPoint to) => item switch
    {
        ArrowAnnotation a => a with { From = from, To = to },
        LineAnnotation l => l with { From = from, To = to },
        _ => item,
    };

    /// <summary>The handles shown on a single selected object, with their positions.</summary>
    public static IReadOnlyList<(EditorHandle Handle, SKPoint Position)> HandlesOf(AnnotationObject item)
    {
        if (EndpointsOf(item) is { } ends)
        {
            return [(EditorHandle.Start, P(ends.From)), (EditorHandle.End, P(ends.To))];
        }

        if (BoxOf(item) is not { } box)
        {
            return [];
        }

        float l = box.X, t = box.Y, r = box.Right, b = box.Bottom, cx = (l + r) / 2, cy = (t + b) / 2;
        return
        [
            (EditorHandle.TopLeft, new SKPoint(l, t)), (EditorHandle.Top, new SKPoint(cx, t)),
            (EditorHandle.TopRight, new SKPoint(r, t)), (EditorHandle.Right, new SKPoint(r, cy)),
            (EditorHandle.BottomRight, new SKPoint(r, b)), (EditorHandle.Bottom, new SKPoint(cx, b)),
            (EditorHandle.BottomLeft, new SKPoint(l, b)), (EditorHandle.Left, new SKPoint(l, cy)),
        ];
    }

    /// <summary>Moves the edges a box handle controls to the point; dragging past the opposite edge flips it.</summary>
    public static PixelRect ResizeBox(PixelRect box, EditorHandle handle, PixelPoint p)
    {
        int left = box.X, top = box.Y, right = box.Right, bottom = box.Bottom;
        if (handle is EditorHandle.Left or EditorHandle.TopLeft or EditorHandle.BottomLeft)
        {
            left = p.X;
        }

        if (handle is EditorHandle.Right or EditorHandle.TopRight or EditorHandle.BottomRight)
        {
            right = p.X;
        }

        if (handle is EditorHandle.Top or EditorHandle.TopLeft or EditorHandle.TopRight)
        {
            top = p.Y;
        }

        if (handle is EditorHandle.Bottom or EditorHandle.BottomLeft or EditorHandle.BottomRight)
        {
            bottom = p.Y;
        }

        return PixelRect.FromCorners(new PixelPoint(left, top), new PixelPoint(right, bottom));
    }

    public static float DistanceToSegment(SKPoint p, SKPoint a, SKPoint b)
    {
        var ab = b - a;
        var lengthSquared = (ab.X * ab.X) + (ab.Y * ab.Y);
        if (lengthSquared < 1e-6f)
        {
            return SKPoint.Distance(p, a);
        }

        var t = Math.Clamp((((p.X - a.X) * ab.X) + ((p.Y - a.Y) * ab.Y)) / lengthSquared, 0, 1);
        return SKPoint.Distance(p, new SKPoint(a.X + (ab.X * t), a.Y + (ab.Y * t)));
    }

    private static float DistanceToPolyline(SKPoint p, IReadOnlyList<PixelPoint> points)
    {
        if (points.Count == 0)
        {
            return float.MaxValue;
        }

        if (points.Count == 1)
        {
            return SKPoint.Distance(p, P(points[0]));
        }

        var best = float.MaxValue;
        for (var i = 1; i < points.Count; i++)
        {
            best = Math.Min(best, DistanceToSegment(p, P(points[i - 1]), P(points[i])));
        }

        return best;
    }

    private static bool HitBox(SKRect box, SKPoint p, bool filled, float strokeWidth, float tolerance)
    {
        var reach = (strokeWidth / 2) + tolerance;
        if (!Inflate(box, reach).Contains(p))
        {
            return false;
        }

        return filled || !Inflate(box, -reach).Contains(p);
    }

    private static bool HitEllipse(SKRect box, SKPoint p, bool filled, float strokeWidth, float tolerance)
    {
        var rx = Math.Max(0.5f, box.Width / 2);
        var ry = Math.Max(0.5f, box.Height / 2);
        var dx = (p.X - box.MidX) / rx;
        var dy = (p.Y - box.MidY) / ry;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy)); // 1 on the outline

        // Approximate the edge band in normalized units using the smaller radius.
        var band = ((strokeWidth / 2) + tolerance) / Math.Min(rx, ry);
        return filled ? distance <= 1 + band : Math.Abs(distance - 1) <= band;
    }

    private static SKPoint P(PixelPoint p) => new(p.X, p.Y);

    private static SKRect R(PixelRect r) => new(r.X, r.Y, r.Right, r.Bottom);

    private static SKRect Around(PixelPoint a, PixelPoint b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));

    private static SKRect Around(IReadOnlyList<PixelPoint> points) => points.Count == 0
        ? SKRect.Empty
        : new SKRect(points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));

    private static SKRect Inflate(SKRect r, float by) => SKRect.Inflate(r, by, by);
}
