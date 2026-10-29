using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace Snare.App.Spikes;

/// <summary>
/// Phase 0 spike: a large image in an SKElement with a draggable arrow, and a paint-time
/// readout in the title bar. Answers "is a Skia canvas in WPF smooth enough at 5120x2880?".
/// </summary>
internal sealed class SkiaCanvasWindow : Window
{
    private const float GrabRadius = 24;

    private readonly SKImage _image;
    private readonly string _label;
    private readonly SKElement _canvas = new();
    private readonly Stopwatch _fpsWindow = Stopwatch.StartNew();
    private SKPoint _from;
    private SKPoint _to;
    private int _dragging; // 0 none, 1 from, 2 to
    private float _scale = 1;
    private SKPoint _offset;
    private int _frames;
    private double _lastPaintMs;

    public SkiaCanvasWindow(SKImage image, string label)
    {
        _image = image;
        _label = label;
        _from = new SKPoint(image.Width * 0.3f, image.Height * 0.3f);
        _to = new SKPoint(image.Width * 0.6f, image.Height * 0.6f);

        Title = "Skia canvas spike";
        Width = 1600;
        Height = 900;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = _canvas;

        _canvas.PaintSurface += OnPaintSurface;
        _canvas.MouseLeftButtonDown += OnMouseDown;
        _canvas.MouseMove += OnMouseMove;
        _canvas.MouseLeftButtonUp += (_, _) =>
        {
            _dragging = 0;
            _canvas.ReleaseMouseCapture();
        };
        Closed += (_, _) => _image.Dispose();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var started = Stopwatch.GetTimestamp();
        var canvas = e.Surface.Canvas;
        canvas.Clear(new SKColor(0x20, 0x20, 0x20));

        _scale = Math.Min((float)e.Info.Width / _image.Width, (float)e.Info.Height / _image.Height);
        _offset = new SKPoint(
            (e.Info.Width - (_image.Width * _scale)) / 2,
            (e.Info.Height - (_image.Height * _scale)) / 2);

        canvas.Save();
        canvas.Translate(_offset);
        canvas.Scale(_scale);
        canvas.DrawImage(_image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        SpikeArrowPainter.Draw(canvas, _from, _to, strokeWidth: 8);
        canvas.Restore();

        _lastPaintMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        _frames++;
        if (_fpsWindow.ElapsedMilliseconds >= 500)
        {
            var fps = _frames * 1000.0 / _fpsWindow.ElapsedMilliseconds;
            Title = string.Create(CultureInfo.InvariantCulture,
                $"Skia canvas spike | {_label} {_image.Width}x{_image.Height} | surface {e.Info.Width}x{e.Info.Height} | {fps:F0} fps | paint {_lastPaintMs:F1} ms");
            _frames = 0;
            _fpsWindow.Restart();
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var p = ToImage(e.GetPosition(_canvas));
        var radius = GrabRadius / _scale;
        _dragging = SKPoint.Distance(p, _from) <= radius ? 1
            : SKPoint.Distance(p, _to) <= radius ? 2
            : 0;
        if (_dragging != 0)
        {
            _canvas.CaptureMouse();
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging == 0)
        {
            return;
        }

        var p = ToImage(e.GetPosition(_canvas));
        if (_dragging == 1)
        {
            _from = p;
        }
        else
        {
            _to = p;
        }

        _canvas.InvalidateVisual();
    }

    /// <summary>WPF DIPs on the element to image pixels, through the surface's pixel scale.</summary>
    private SKPoint ToImage(Point dip)
    {
        var pixelsPerDip = _canvas.CanvasSize.Width / Math.Max(1, _canvas.ActualWidth);
        var surface = new SKPoint((float)(dip.X * pixelsPerDip), (float)(dip.Y * pixelsPerDip));
        return new SKPoint((surface.X - _offset.X) / _scale, (surface.Y - _offset.Y) / _scale);
    }
}
