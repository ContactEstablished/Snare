using CommunityToolkit.Mvvm.ComponentModel;
using Snare.Core.Hotkeys;
using Snare.Core.Options;

namespace Snare.App.Settings;

/// <summary>One hotkey row in Settings: the action and its gesture text (empty = off).</summary>
public sealed partial class HotkeyItemViewModel(HotkeyAction action, string? gesture) : ObservableObject
{
    public HotkeyAction Action { get; } = action;

    public string Label { get; } = HotkeyOptions.Describe(action);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    public partial string Gesture { get; set; } = gesture ?? "";

    public string DisplayText => Gesture.Length == 0 ? "Off" : Gesture;

    /// <summary>A key press from the view: a usable combination replaces the gesture; Backspace or Delete turns it off.</summary>
    public void Press(HotkeyGesture? gesture, bool clear)
    {
        if (clear)
        {
            Gesture = "";
        }
        else if (gesture is not null)
        {
            Gesture = gesture.ToString();
        }
    }
}
