using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Interop;

namespace Snare.App.QuickBar;

/// <summary>View wiring only: placement in physical pixels near the selection, and auto-dismiss.</summary>
internal sealed partial class QuickBarWindow : Window
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(6);

    private readonly DispatcherTimer _timer = new() { Interval = Lifetime };
    private readonly PixelRect _selection;
    private readonly MonitorInfo _monitor;

    public QuickBarWindow(QuickBarViewModel viewModel, PixelRect selection, MonitorInfo monitor)
    {
        InitializeComponent();
        DataContext = viewModel;
        _selection = selection;
        _monitor = monitor;

        // Rough DIP position first so it doesn't flash elsewhere; Loaded corrects it in physical pixels.
        Left = selection.X / monitor.Scale;
        Top = selection.Bottom / monitor.Scale;

        viewModel.CloseRequested += (_, _) => Close();
        _timer.Tick += (_, _) =>
        {
            if (!IsMouseOver && !IsKeyboardFocusWithin)
            {
                Close();
            }
        };
        MouseLeave += (_, _) => _timer.Start();
        LostKeyboardFocus += (_, _) => _timer.Start();

        // Never part of a screenshot, even if a capture starts while it is still showing.
        SourceInitialized += (_, _) => CaptureExclusion.ExcludeFromCapture(new WindowInteropHelper(this).Handle);
        Loaded += (_, _) =>
        {
            var bar = new PixelSize((int)Math.Ceiling(ActualWidth * _monitor.Scale), (int)Math.Ceiling(ActualHeight * _monitor.Scale));
            var at = QuickBarPlacement.Place(bar, _selection, _monitor.WorkArea);
            WindowPlacement.MoveTopmost(new WindowInteropHelper(this).Handle, at.X, at.Y);
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }
}
