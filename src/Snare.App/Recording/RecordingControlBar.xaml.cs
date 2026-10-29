using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Interop;

namespace Snare.App.Recording;

/// <summary>
/// View wiring for the control bar: kept out of the recording, placed just above the recorded
/// area (or inside its top edge), ticking the clock, and draggable.
/// </summary>
internal sealed partial class RecordingControlBar : Window
{
    private const int Gap = 10;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public RecordingControlBar(RecordingControlViewModel viewModel, PixelRect area, MonitorInfo monitor)
    {
        InitializeComponent();
        DataContext = viewModel;

        _clock.Tick += (_, _) => viewModel.Tick();
        SourceInitialized += (_, _) => CaptureExclusion.ExcludeFromCapture(new WindowInteropHelper(this).Handle);
        Loaded += (_, _) =>
        {
            Place(area, monitor);
            _clock.Start();
        };
        Closed += (_, _) => _clock.Stop();
        Chrome.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        };
    }

    private void Place(PixelRect area, MonitorInfo monitor)
    {
        var width = (int)Math.Ceiling(ActualWidth * monitor.Scale);
        var height = (int)Math.Ceiling(ActualHeight * monitor.Scale);
        var work = monitor.WorkArea;
        var x = Math.Max(work.X + Gap, Math.Min(area.X + ((area.Width - width) / 2), work.Right - width - Gap));
        var y = area.Y - height - Gap >= work.Y ? area.Y - height - Gap : Math.Max(work.Y, area.Y) + Gap;
        WindowPlacement.MoveTopmost(new WindowInteropHelper(this).Handle, x, y);
    }
}
