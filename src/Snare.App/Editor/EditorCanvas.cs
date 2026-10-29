using System.Windows;
using System.Windows.Input;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using Snare.Core.Annotations;
using Snare.Core.Geometry;
using Snare.Rendering.Editing;

namespace Snare.App.Editor;

/// <summary>
/// The editor's drawing surface. It owns only the view transform (zoom and pan) and converts
/// pointer input to image pixels; drawing goes through the shared AnnotationRenderer.
/// </summary>
internal sealed class EditorCanvas : SKElement
{
    private const float HandleSize = 9;
    private const float ScreenTolerance = 5;
    private const double MinZoom = 0.05;
    private const double MaxZoom = 16;

    private static readonly SKColor Background = new(0x1E, 0x1E, 0x1E);
    private static readonly SKColor Accent = new(0x00, 0xB4, 0xFF);

    private readonly EditorViewModel _viewModel;
    private float _scale = 1;          // surface pixels per image pixel
    private SKPoint _origin;           // image (0, 0) in surface pixels
    private float _pixelsPerDip = 1;
    private bool _fitted;
    private bool _panning;
    private Point _panStart;
    private SKPoint _panOrigin;

    public EditorCanvas(EditorViewModel viewModel)
    {
        _viewModel = viewModel;
        Focusable = true;
        ClipToBounds = true;
        viewModel.Session.Changed += (_, _) => InvalidateVisual();
        SizeChanged += (_, _) =>
        {
            if (!_fitted)
            {
                InvalidateVisual();
            }
        };
    }

    /// <summary>Held by the window while Space is down: dragging pans instead of drawing.</summary>
    public bool SpaceHeld { get; set; }

    /// <summary>Image pixels to DIPs on this element, for placing the text box.</summary>
    public Point ImageToDip(PixelPoint p) =>
        new((_origin.X + (p.X * _scale)) / _pixelsPerDip, (_origin.Y + (p.Y * _scale)) / _pixelsPerDip);

    /// <summary>DIPs per image pixel at the current zoom.</summary>
    public double DipsPerImagePixel => _scale / _pixelsPerDip;

    public void ZoomToFit()
    {
        var size = CanvasSize;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var image = _viewModel.Original;
        const float margin = 32;
        _scale = Math.Min(1, Math.Min((size.Width - margin) / image.Width, (size.Height - margin) / image.Height));
        _scale = Math.Max((float)MinZoom, _scale);
        _origin = new SKPoint((size.Width - (image.Width * _scale)) / 2, (size.Height - (image.Height * _scale)) / 2);
        _fitted = true;
        AfterZoom();
    }

    public void ZoomToActualSize() => ZoomAround(new SKPoint(CanvasSize.Width / 2, CanvasSize.Height / 2), 1 / _scale);

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);
        _pixelsPerDip = ActualWidth > 0 ? (float)(e.Info.Width / ActualWidth) : 1;
        if (!_fitted && e.Info.Width > 0)
        {
            ZoomToFit();
        }

        var canvas = e.Surface.Canvas;
        canvas.Clear(Background);

        var session = _viewModel.Session;
        var document = session.ToDocument();
        canvas.Save();
        canvas.Translate(_origin);
        canvas.Scale(_scale);

        _viewModel.Renderer.Draw(canvas, _viewModel.OriginalImage, document,
            _scale >= 2 ? new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None) : null);
        if (session.Preview is { } preview)
        {
            _viewModel.Renderer.DrawObjects(canvas, _viewModel.OriginalImage,
                new AnnotationDocument { Canvas = document.Canvas, Objects = [preview] });
        }

        DrawCropShade(canvas, session.PendingCrop ?? document.Canvas.Crop, session.PendingCrop is not null);
        DrawSelection(canvas, session);
        canvas.Restore();
    }

    private void DrawCropShade(SKCanvas canvas, PixelRect? crop, bool editing)
    {
        if (crop is not { } area)
        {
            return;
        }

        var image = new SKRect(0, 0, _viewModel.Original.Width, _viewModel.Original.Height);
        var keep = new SKRect(area.X, area.Y, area.Right, area.Bottom);
        using var shade = new SKPaint { Color = new SKColor(0, 0, 0, 0xA0) };
        canvas.Save();
        canvas.ClipRect(keep, SKClipOperation.Difference);
        canvas.DrawRect(image, shade);
        canvas.Restore();

        using var frame = new SKPaint
        {
            Color = editing ? Accent : SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f / _scale,
            IsAntialias = true,
            PathEffect = editing ? null : SKPathEffect.CreateDash([6 / _scale, 4 / _scale], 0),
        };
        canvas.DrawRect(keep, frame);
    }

    private void DrawSelection(SKCanvas canvas, EditorSession session)
    {
        using var outline = new SKPaint
        {
            Color = Accent,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f / _scale,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash([5 / _scale, 3 / _scale], 0),
        };
        var selected = session.SelectedItems;
        foreach (var item in selected)
        {
            canvas.DrawRect(SKRect.Inflate(AnnotationGeometry.Bounds(item), 3 / _scale, 3 / _scale), outline);
        }

        if (selected.Count == 1)
        {
            using var fill = new SKPaint { Color = SKColors.White, IsAntialias = true };
            using var border = new SKPaint { Color = Accent, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f / _scale, IsAntialias = true };
            var half = HandleSize / 2 / _scale;
            foreach (var (_, position) in AnnotationGeometry.HandlesOf(selected[0]))
            {
                var r = new SKRect(position.X - half, position.Y - half, position.X + half, position.Y + half);
                canvas.DrawRect(r, fill);
                canvas.DrawRect(r, border);
            }
        }

        if (session.Marquee is { } marquee)
        {
            using var band = new SKPaint { Color = Accent.WithAlpha(0x30) };
            canvas.DrawRect(marquee, band);
            outline.PathEffect = null;
            canvas.DrawRect(marquee, outline);
        }
    }

    // ---- Input ----------------------------------------------------------------------------

    private SKPoint ToImage(Point dip) =>
        new((float)(((dip.X * _pixelsPerDip) - _origin.X) / _scale), (float)(((dip.Y * _pixelsPerDip) - _origin.Y) / _scale));

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        _viewModel.Session.HitTolerance = ScreenTolerance * _pixelsPerDip / _scale;

        if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && SpaceHeld))
        {
            _panning = true;
            _panStart = e.GetPosition(this);
            _panOrigin = _origin;
            CaptureMouse();
            Cursor = Cursors.ScrollAll;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var point = ToImage(e.GetPosition(this));
        if (e.ClickCount == 2)
        {
            _viewModel.Session.DoubleClick(point);
            return;
        }

        CaptureMouse();
        _viewModel.Session.PointerDown(point, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_panning)
        {
            var now = e.GetPosition(this);
            _origin = new SKPoint(
                _panOrigin.X + (float)((now.X - _panStart.X) * _pixelsPerDip),
                _panOrigin.Y + (float)((now.Y - _panStart.Y) * _pixelsPerDip));
            InvalidateVisual();
            return;
        }

        var point = ToImage(e.GetPosition(this));
        if (IsMouseCaptured)
        {
            _viewModel.Session.PointerMove(point, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        }

        UpdateCursor(point);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (_panning)
        {
            _panning = false;
            ReleaseMouseCapture();
            UpdateCursor(ToImage(e.GetPosition(this)));
            return;
        }

        if (e.ChangedButton == MouseButton.Left && IsMouseCaptured)
        {
            ReleaseMouseCapture();
            _viewModel.Session.PointerUp(ToImage(e.GetPosition(this)), Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        }
    }

    /// <summary>Alt+Tab or another app stealing the mouse mid-drag: keep the move as an undoable step.</summary>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_panning)
        {
            _panning = false;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed || Mouse.LeftButton == MouseButtonState.Pressed)
        {
            _viewModel.Session.PointerCancel();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        var dip = e.GetPosition(this);
        var at = new SKPoint((float)(dip.X * _pixelsPerDip), (float)(dip.Y * _pixelsPerDip));
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ZoomAround(at, Math.Pow(1.15, e.Delta / 120.0));
        }
        else
        {
            var step = (float)(e.Delta * _pixelsPerDip);
            _origin = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                ? new SKPoint(_origin.X + step, _origin.Y)
                : new SKPoint(_origin.X, _origin.Y + step);
            InvalidateVisual();
        }

        e.Handled = true;
    }

    private void ZoomAround(SKPoint surfacePoint, double factor)
    {
        var next = (float)Math.Clamp(_scale * factor, MinZoom, MaxZoom);
        var imagePoint = new SKPoint((surfacePoint.X - _origin.X) / _scale, (surfacePoint.Y - _origin.Y) / _scale);
        _scale = next;
        _origin = new SKPoint(surfacePoint.X - (imagePoint.X * _scale), surfacePoint.Y - (imagePoint.Y * _scale));
        AfterZoom();
    }

    private void AfterZoom()
    {
        // 100% means one image pixel per screen pixel (what Ctrl+1 shows).
        _viewModel.ZoomText = $"{Math.Round(_scale * 100)}%";
        InvalidateVisual();
    }

    private void UpdateCursor(SKPoint point)
    {
        if (SpaceHeld)
        {
            Cursor = Cursors.Hand;
            return;
        }

        var session = _viewModel.Session;
        session.HitTolerance = ScreenTolerance * _pixelsPerDip / _scale;
        Cursor = session.Tool switch
        {
            EditorTool.Select => session.HandleAt(point) switch
            {
                EditorHandle.Body => Cursors.SizeAll,
                EditorHandle.Left or EditorHandle.Right => Cursors.SizeWE,
                EditorHandle.Top or EditorHandle.Bottom => Cursors.SizeNS,
                EditorHandle.TopLeft or EditorHandle.BottomRight => Cursors.SizeNWSE,
                EditorHandle.TopRight or EditorHandle.BottomLeft => Cursors.SizeNESW,
                EditorHandle.Start or EditorHandle.End => Cursors.Cross,
                _ => Cursors.Arrow,
            },
            EditorTool.Text => Cursors.IBeam,
            EditorTool.Eyedropper or EditorTool.Step => Cursors.Hand,
            _ => Cursors.Cross,
        };
    }
}
