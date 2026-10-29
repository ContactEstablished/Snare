using CommunityToolkit.Mvvm.ComponentModel;

namespace Snare.App.Library;

/// <summary>A tag shown as a chip in the detail pane.</summary>
public sealed partial class TagChipViewModel(string name, string color) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    public partial string Color { get; set; } = color;
}
