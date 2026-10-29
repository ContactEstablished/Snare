using System.Windows;

namespace Snare.App.Editor;

/// <summary>View wiring only: the dialog closes when the view model gives a result.</summary>
internal sealed partial class ResizeWindow : Window
{
    public ResizeWindow(ResizeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Closed += (_, size) =>
        {
            Result = size;
            Close();
        };
    }

    /// <summary>The chosen size, or null when cancelled.</summary>
    public Snare.Core.Geometry.PixelSize? Result { get; private set; }
}
