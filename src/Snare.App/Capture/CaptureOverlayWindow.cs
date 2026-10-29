using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snare.App.Infrastructure;
using Snare.Capture.Selection;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Interop;

namespace Snare.App.Capture;

/// <summary>
/// One monitor's capture overlay: its frozen frame 1:1 plus <see cref="OverlayChrome"/>.
/// Only view wiring: input goes to the view model as physical cursor positions.
/// </summary>
internal sealed class CaptureOverlayWindow : Window
{
    private readonly CaptureOverlayViewModel _viewModel;
    private readonly OverlayChrome _chrome;

    public CaptureOverlayWindow(CaptureOverlayViewModel viewModel, FrozenMonitor monitor)
    {
        _viewModel = viewModel;
        _chrome = new OverlayChrome(viewModel, monitor);

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Black;
        Cursor = Cursors.Cross;
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

        var root = new Grid();
        root.Children.Add(image);
        root.Children.Add(_chrome);
        Content = root;

        PhysicalPlacement.Pin(this, monitor.Monitor.Bounds);

        viewModel.Redraw += OnRedraw;
        Closed += (_, _) => viewModel.Redraw -= OnRedraw;
    }

    private static PixelPoint Pointer()
    {
        var (x, y) = WindowPlacement.GetCursorPosition();
        return new PixelPoint(x, y);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _viewModel.PointerMove(Pointer());
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ClickCount == 2)
        {
            _viewModel.DoubleClick(Pointer());
            return;
        }

        CaptureMouse();
        _viewModel.PointerDown(Pointer());
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ReleaseMouseCapture();
        _viewModel.PointerUp(Pointer());
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        _viewModel.CancelCommand.Execute(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Handled here, not with KeyBindings: WPF's KeyGesture rejects bare letter keys.
        var command = e.Key switch
        {
            Key.Escape => _viewModel.CancelCommand,
            Key.Enter => _viewModel.AcceptCommand,
            Key.F => _viewModel.TakeMonitorCommand,
            Key.A => _viewModel.TakeAllMonitorsCommand,
            _ => null,
        };
        if (command is not null)
        {
            command.Execute(null);
            e.Handled = true;
            return;
        }

        var large = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        (int X, int Y)? step = e.Key switch
        {
            Key.Left => (-1, 0),
            Key.Right => (1, 0),
            Key.Up => (0, -1),
            Key.Down => (0, 1),
            _ => null,
        };

        if (step is { } s)
        {
            _viewModel.Nudge(s.X, s.Y, large);
            e.Handled = true;
        }
    }

    private void OnRedraw(object? sender, EventArgs e)
    {
        _chrome.InvalidateVisual();
        Cursor = _viewModel.Session.HandleAt(_viewModel.Session.Pointer) switch
        {
            SelectionHandle.Inside => Cursors.SizeAll,
            SelectionHandle.Left or SelectionHandle.Right => Cursors.SizeWE,
            SelectionHandle.Top or SelectionHandle.Bottom => Cursors.SizeNS,
            SelectionHandle.TopLeft or SelectionHandle.BottomRight => Cursors.SizeNWSE,
            SelectionHandle.TopRight or SelectionHandle.BottomLeft => Cursors.SizeNESW,
            _ => Cursors.Cross,
        };
    }
}
