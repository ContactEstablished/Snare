using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snare.App.Infrastructure;
using Snare.Capture.Selection;
using Snare.Core.Captures;
using Snare.Core.Geometry;

namespace Snare.App.Capture;

/// <summary>
/// Draws one monitor's overlay chrome over the frozen frame: the dimmed surround, the highlight,
/// handles, crosshair, loupe and readout. Pure view code; state comes from the view model.
/// </summary>
internal sealed class OverlayChrome : FrameworkElement
{
    private const int LoupePixels = 15;
    private const double LoupeZoom = 8;
    private const double Gap = 20;

    private static readonly Brush Dim = Frozen(new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)));
    private static readonly Brush Accent = Frozen(new SolidColorBrush(Color.FromRgb(0x00, 0xB4, 0xFF)));
    private static readonly Brush Panel = Frozen(new SolidColorBrush(Color.FromArgb(0xE6, 0x16, 0x16, 0x16)));
    private static readonly Pen AccentPen = Frozen(new Pen(Accent, 2));
    private static readonly Pen WindowPen = Frozen(new Pen(Accent, 4));
    private static readonly Pen HandlePen = Frozen(new Pen(Accent, 1));
    private static readonly Pen CrosshairPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0xA0, 0xFF, 0xFF, 0xFF)), 1));
    private static readonly Pen LoupeCenterPen = Frozen(new Pen(Brushes.White, 1));
    private static readonly Typeface Mono = new("Consolas");
    private static readonly Typeface Ui = new("Segoe UI");

    private readonly CaptureOverlayViewModel _viewModel;
    private readonly FrozenMonitor _monitor;
    private readonly WriteableBitmap _loupe = new(LoupePixels, LoupePixels, 96, 96, PixelFormats.Bgra32, null);
    private readonly uint[] _loupePixels = new uint[LoupePixels * LoupePixels];

    public OverlayChrome(CaptureOverlayViewModel viewModel, FrozenMonitor monitor)
    {
        _viewModel = viewModel;
        _monitor = monitor;
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    private PixelRect Bounds => _monitor.Monitor.Bounds;

    protected override void OnRender(DrawingContext dc)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var scale = dpi.DpiScaleX;
        var session = _viewModel.Session;
        var full = new Rect(0, 0, ActualWidth, ActualHeight);

        // Dim everything except the highlight.
        Geometry dimmed = new RectangleGeometry(full);
        if (session.Highlight is { } highlight)
        {
            var hole = PhysicalPlacement.ToLocalDip(highlight, Bounds, scale);
            dimmed = new CombinedGeometry(GeometryCombineMode.Exclude, dimmed, new RectangleGeometry(hole));
            dc.DrawGeometry(Dim, null, dimmed);
            dc.DrawRectangle(null, session.Selection is null ? WindowPen : AccentPen, hole);

            if (session.Phase is SelectionPhase.Adjusting or SelectionPhase.Moving or SelectionPhase.Resizing)
            {
                DrawHandles(dc, hole);
            }

            if (_viewModel.SizeText is { } size)
            {
                var label = Text(size, Ui, 13, Brushes.White, dpi.PixelsPerDip);
                var origin = new Point(hole.X, Math.Max(0, hole.Y - label.Height - 8));
                dc.DrawRoundedRectangle(Panel, null, new Rect(origin.X, origin.Y, label.Width + 12, label.Height + 4), 3, 3);
                dc.DrawText(label, new Point(origin.X + 6, origin.Y + 2));
            }
        }
        else
        {
            dc.DrawGeometry(Dim, null, dimmed);
        }

        if (!Bounds.Contains(session.Pointer))
        {
            return;
        }

        var pointer = PhysicalPlacement.ToLocalDip(session.Pointer, Bounds, scale);
        if (session.Phase is SelectionPhase.Hovering or SelectionPhase.Drawing)
        {
            dc.DrawLine(CrosshairPen, new Point(0, pointer.Y + 0.5), new Point(ActualWidth, pointer.Y + 0.5));
            dc.DrawLine(CrosshairPen, new Point(pointer.X + 0.5, 0), new Point(pointer.X + 0.5, ActualHeight));
        }

        DrawLoupe(dc, pointer, dpi.PixelsPerDip);

        if (session.Phase == SelectionPhase.Hovering)
        {
            DrawHint(dc, dpi.PixelsPerDip);
        }
    }

    private static void DrawHandles(DrawingContext dc, Rect r)
    {
        const double size = 8;
        foreach (var (x, y) in new[]
        {
            (r.Left, r.Top), (r.Left + (r.Width / 2), r.Top), (r.Right, r.Top),
            (r.Left, r.Top + (r.Height / 2)), (r.Right, r.Top + (r.Height / 2)),
            (r.Left, r.Bottom), (r.Left + (r.Width / 2), r.Bottom), (r.Right, r.Bottom),
        })
        {
            dc.DrawRectangle(Brushes.White, HandlePen, new Rect(x - (size / 2), y - (size / 2), size, size));
        }
    }

    private void DrawLoupe(DrawingContext dc, Point pointer, double pixelsPerDip)
    {
        UpdateLoupePixels();

        var lines = Text(
            $"{_viewModel.PositionText}\n{_viewModel.ColorText}" + (_viewModel.SizeText is { } s ? $"\n{s}" : ""),
            Mono, 13, Brushes.White, pixelsPerDip);
        var loupeSize = LoupePixels * LoupeZoom;
        var panel = new Size(loupeSize + 12, loupeSize + lines.Height + 18);

        // Below-right of the pointer, flipped when it would run off this monitor.
        var x = pointer.X + Gap + panel.Width > ActualWidth ? pointer.X - Gap - panel.Width : pointer.X + Gap;
        var y = pointer.Y + Gap + panel.Height > ActualHeight ? pointer.Y - Gap - panel.Height : pointer.Y + Gap;

        dc.DrawRoundedRectangle(Panel, null, new Rect(x, y, panel.Width, panel.Height), 4, 4);
        var loupeRect = new Rect(x + 6, y + 6, loupeSize, loupeSize);
        dc.DrawImage(_loupe, loupeRect);
        var center = (LoupePixels / 2) * LoupeZoom;
        dc.DrawRectangle(null, LoupeCenterPen, new Rect(loupeRect.X + center, loupeRect.Y + center, LoupeZoom, LoupeZoom));
        dc.DrawText(lines, new Point(x + 6, loupeRect.Bottom + 6));
    }

    private void UpdateLoupePixels()
    {
        var p = _viewModel.Session.Pointer;
        var half = LoupePixels / 2;
        for (var row = 0; row < LoupePixels; row++)
        {
            for (var col = 0; col < LoupePixels; col++)
            {
                _loupePixels[(row * LoupePixels) + col] =
                    _viewModel.Desktop.GetArgb(new PixelPoint(p.X - half + col, p.Y - half + row)) ?? 0xFF000000;
            }
        }

        _loupe.WritePixels(new Int32Rect(0, 0, LoupePixels, LoupePixels), _loupePixels, LoupePixels * 4, 0);
    }

    private void DrawHint(DrawingContext dc, double pixelsPerDip)
    {
        var hint = Text(
            "Drag a region  ·  Click a window  ·  F this monitor  ·  A all monitors  ·  Enter accept  ·  Esc cancel",
            Ui, 14, Brushes.White, pixelsPerDip);
        var rect = new Rect((ActualWidth - hint.Width) / 2 - 12, 16, hint.Width + 24, hint.Height + 10);
        dc.DrawRoundedRectangle(Panel, null, rect, 6, 6);
        dc.DrawText(hint, new Point(rect.X + 12, rect.Y + 5));
    }

    private static FormattedText Text(string text, Typeface typeface, double size, Brush brush, double pixelsPerDip) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, brush, pixelsPerDip);

    private static T Frozen<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
