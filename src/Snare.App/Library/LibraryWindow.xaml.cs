using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Snare.App.Library;

/// <summary>View wiring only.</summary>
internal sealed partial class LibraryWindow : Window
{
    public LibraryWindow(LibraryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
        Closed += (_, _) => viewModel.Detach();
        Grid.PreviewMouseRightButtonDown += SelectTileUnderPointer;
    }

    /// <summary>
    /// The context menu acts on the selected tile, so right-click selects the tile under the
    /// pointer first, and right-clicking empty space clears the selection.
    /// </summary>
    private void SelectTileUnderPointer(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        while (element is not null and not ListBoxItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        Grid.SelectedItem = (element as ListBoxItem)?.DataContext;
    }
}
