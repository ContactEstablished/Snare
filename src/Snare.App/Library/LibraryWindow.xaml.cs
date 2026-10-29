using System.Windows;

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
    }
}
