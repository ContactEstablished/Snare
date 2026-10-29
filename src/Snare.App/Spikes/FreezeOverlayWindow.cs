using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Interop;

namespace Snare.App.Spikes;

/// <summary>
/// Phase 0 spike: one borderless topmost window per monitor showing that monitor's frozen frame
/// 1:1, with a crosshair and a readout of the physical cursor position and frozen pixel color.
/// Phase 1 replaces this with the real capture overlay.
/// </summary>
internal sealed class FreezeOverlayWindow : Window
{
    private readonly FrozenDesktop _desktop;
    private readonly FrozenMonitor _monitor;
    private readonly Line _horizontal = CrosshairLine();
    private readonly Line _vertical = CrosshairLine();
    private readonly TextBlock _readout = new()
    {
        Foreground = Brushes.White,
        Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x10, 0x10, 0x10)),
        Padding = new Thickness(6, 3, 6, 3),
        FontFamily = new FontFamily("Consolas"),
        FontSize = 13,
        IsHitTestVisible = false,
    };

    public FreezeOverlayWindow(FrozenDesktop desktop, FrozenMonitor monitor)
    {
        _desktop = desktop;
        _monitor = monitor;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Black;
        Cursor = Cursors.None;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = monitor.Monitor.Bounds.X;
        Top = monitor.Monitor.Bounds.Y;
        Width = monitor.Monitor.Bounds.Width;
        Height = monitor.Monitor.Bounds.Height;

        var frame = monitor.Frame;
        var image = new Image
        {
            Source = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null, frame.Pixels, frame.Stride),
            Stretch = Stretch.Fill,
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

        var canvas = new Canvas();
        canvas.Children.Add(_horizontal);
        canvas.Children.Add(_vertical);
        canvas.Children.Add(_readout);

        var root = new Grid();
        root.Children.Add(image);
        root.Children.Add(canvas);
        Content = root;

        SourceInitialized += (_, _) => PlaceOnMonitor();
        MouseMove += (_, e) => UpdateCrosshair(e.GetPosition(canvas));
    }

    private void PlaceOnMonitor()
    {
        var b = _monitor.Monitor.Bounds;
        WindowPlacement.SetTopmostPhysicalBounds(new WindowInteropHelper(this).Handle, b.X, b.Y, b.Width, b.Height);
    }

    private void UpdateCrosshair(Point dip)
    {
        _horizontal.X1 = 0;
        _horizontal.X2 = ActualWidth;
        _horizontal.Y1 = _horizontal.Y2 = dip.Y;
        _vertical.Y1 = 0;
        _vertical.Y2 = ActualHeight;
        _vertical.X1 = _vertical.X2 = dip.X;

        var (x, y) = WindowPlacement.GetCursorPosition();
        var argb = _desktop.GetArgb(new PixelPoint(x, y));
        var color = argb is { } c ? $"#{c & 0xFFFFFF:X6}" : "off-screen";
        _readout.Text = string.Create(CultureInfo.InvariantCulture,
            $"FROZEN  {_monitor.Monitor.DeviceName}  {_monitor.Monitor.Dpi} dpi\nx {x}  y {y}  {color}\nEsc close   Ctrl+S Skia canvas");

        Canvas.SetLeft(_readout, dip.X + 16);
        Canvas.SetTop(_readout, dip.Y + 16);
    }

    private static Line CrosshairLine() => new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(0xC0, 0x00, 0xE5, 0xFF)),
        StrokeThickness = 1,
        IsHitTestVisible = false,
        SnapsToDevicePixels = true,
    };
}
