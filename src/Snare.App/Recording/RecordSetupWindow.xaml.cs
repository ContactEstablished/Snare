using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Snare.Core.Recording;

namespace Snare.App.Recording;

/// <summary>View wiring only: highlights the chosen corner, and closing the window cancels.</summary>
internal sealed partial class RecordSetupWindow : Window
{
    public static readonly IValueConverter Not = new NotConverter();

    private static readonly Brush Chosen = new SolidColorBrush(Color.FromRgb(0x00, 0xB4, 0xFF));
    private static readonly Brush Other = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));

    private readonly RecordSetupViewModel _viewModel;
    private bool _closed;

    public RecordSetupWindow(RecordSetupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RecordSetupViewModel.WebcamCorner))
            {
                ShowCorner();
            }
        };
        ShowCorner();
        _ = viewModel.Result.ContinueWith(_ => Dispatcher.BeginInvoke(() =>
        {
            if (!_closed)
            {
                Close();
            }
        }), TaskScheduler.Default);
        Closed += (_, _) => _closed = true;
        Closing += OnClosing;
    }

    private void ShowCorner()
    {
        foreach (var (button, corner) in new (Button, WebcamCorner)[]
        {
            (TopLeft, WebcamCorner.TopLeft), (TopRight, WebcamCorner.TopRight),
            (BottomLeft, WebcamCorner.BottomLeft), (BottomRight, WebcamCorner.BottomRight),
        })
        {
            button.Background = corner == _viewModel.WebcamCorner ? Chosen : Other;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _closed = true;
        _viewModel.Cancel(); // no-op once a choice was made
    }

    private sealed class NotConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    }
}
