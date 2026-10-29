using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Interop;

namespace Snare.App.Scrolling;

/// <summary>
/// View wiring for the scroll panel: kept out of captures, placed beside the area on its monitor,
/// draggable, Esc cancels, and the preview bitmap drawn from the view model's pixels.
/// </summary>
internal sealed partial class ScrollPanelWindow : Window
{
    private const int Gap = 12;
    private readonly ScrollPanelViewModel _viewModel;

    public ScrollPanelWindow(ScrollPanelViewModel viewModel, PixelRect area, MonitorInfo monitor)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        SourceInitialized += (_, _) => CaptureExclusion.ExcludeFromCapture(new WindowInteropHelper(this).Handle);
        Loaded += (_, _) => Place(area, monitor);
        viewModel.PropertyChanged += OnViewModelChanged;
        Closed += (_, _) => viewModel.PropertyChanged -= OnViewModelChanged;
        Chrome.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && _viewModel.CancelCommand.CanExecute(null))
        {
            _viewModel.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScrollPanelViewModel.Preview) && _viewModel.Preview is { } tail)
        {
            // Bgr32 ignores the alpha byte, which GDI grabs leave at zero.
            var bitmap = BitmapSource.Create(tail.Width, tail.Height, 96, 96, PixelFormats.Bgr32, null, tail.Pixels, tail.Stride);
            bitmap.Freeze();
            PreviewImage.Source = bitmap;
        }
    }

    /// <summary>To the right of the area, else to its left, else inside its right edge; always on its monitor.</summary>
    private void Place(PixelRect area, MonitorInfo monitor)
    {
        var width = (int)Math.Ceiling(ActualWidth * monitor.Scale);
        var height = (int)Math.Ceiling(ActualHeight * monitor.Scale);
        var work = monitor.WorkArea;
        var x = area.Right + Gap + width <= work.Right ? area.Right + Gap
            : area.X - Gap - width >= work.X ? area.X - Gap - width
            : Math.Max(work.X, Math.Min(area.Right, work.Right) - width - Gap);
        var y = Math.Max(work.Y + Gap, Math.Min(area.Y, work.Bottom - height - Gap));
        WindowPlacement.MoveTopmost(new WindowInteropHelper(this).Handle, x, y);
    }
}
