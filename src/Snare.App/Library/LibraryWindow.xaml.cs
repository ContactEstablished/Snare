using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Snare.App.Library;

/// <summary>View wiring only: selection forwarding, right-click selection, focus, and the Empty Trash prompt.</summary>
internal sealed partial class LibraryWindow : Window
{
    public static readonly RoutedCommand FocusSearch = new();

    /// <summary>Shows the "select a capture" hint when nothing is selected.</summary>
    public static readonly IValueConverter ZeroToVisible = new ZeroToVisibleConverter();

    private readonly LibraryViewModel _viewModel;

    public LibraryWindow(LibraryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.ConfirmEmptyTrash = count => MessageBox.Show(
            this,
            $"Permanently delete {count} capture{(count == 1 ? "" : "s")} and their files? This can't be undone.",
            "Empty Trash",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel) == MessageBoxResult.OK;

        CommandBindings.Add(new CommandBinding(FocusSearch, (_, _) => SearchBox.Focus()));
        Grid.SelectionChanged += (_, _) => viewModel.UpdateSelection(Grid.SelectedItems.Cast<CaptureTileViewModel>().ToList());
        Grid.PreviewMouseRightButtonDown += SelectTileUnderPointer;

        // Title and description save when focus leaves them.
        TitleBox.LostKeyboardFocus += async (_, _) => await SaveDetailsAsync();
        DescriptionBox.LostKeyboardFocus += async (_, _) => await SaveDetailsAsync();

        Loaded += async (_, _) =>
        {
            await viewModel.LoadAsync();
            SearchBox.Focus();
        };
        Closed += (_, _) => viewModel.Detach();
    }

    private Task SaveDetailsAsync() => _viewModel.Detail?.SaveDetailsAsync() ?? Task.CompletedTask;

    /// <summary>
    /// The context menu acts on the selection, so right-clicking a tile outside the selection
    /// selects just it, and right-clicking empty space clears the selection.
    /// </summary>
    private void SelectTileUnderPointer(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        while (element is not null and not ListBoxItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        if (element is ListBoxItem { IsSelected: true })
        {
            return;
        }

        Grid.SelectedItems.Clear();
        if (element is ListBoxItem item)
        {
            item.IsSelected = true;
        }
    }

    private sealed class ZeroToVisibleConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is 0 ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
