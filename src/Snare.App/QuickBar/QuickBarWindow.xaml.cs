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
    private const int Gap = 12;

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
            if (!IsMouseOver)
            {
                Close();
            }
        };
        MouseLeave += (_, _) => _timer.Start();
        Loaded += (_, _) =>
        {
            PlaceNearSelection();
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    /// <summary>Below the selection's bottom-right, kept inside the monitor's work area.</summary>
    private void PlaceNearSelection()
    {
        var width = (int)Math.Ceiling(ActualWidth * _monitor.Scale);
        var height = (int)Math.Ceiling(ActualHeight * _monitor.Scale);
        var work = _monitor.WorkArea;

        var x = Math.Max(work.X + Gap, Math.Min(_selection.Right - width, work.Right - width - Gap));
        var y = _selection.Bottom + Gap;
        if (y + height > work.Bottom - Gap)
        {
            // No room below: tuck it inside the selection's bottom edge instead.
            y = Math.Max(work.Y + Gap, Math.Min(_selection.Bottom, work.Bottom) - height - Gap);
        }

        WindowPlacement.MoveTopmost(new WindowInteropHelper(this).Handle, x, y);
    }
}
