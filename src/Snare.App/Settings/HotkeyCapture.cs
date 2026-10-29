using System.Windows.Input;
using Snare.Core.Hotkeys;

namespace Snare.App.Settings;

/// <summary>Turns a key press in a hotkey box into a gesture.</summary>
public static class HotkeyCapture
{
    /// <summary>
    /// The gesture for <paramref name="key"/> with <paramref name="modifiers"/>, or null for a key
    /// a hotkey can't use (a modifier on its own while Matt is still pressing the combination, or a
    /// numpad digit, which would register the top-row key instead).
    /// Pass <c>e.Key == Key.System ? e.SystemKey : e.Key</c>, since Alt combinations arrive as System.
    /// </summary>
    public static HotkeyGesture? FromKey(Key key, ModifierKeys modifiers)
    {
        var name = key switch
        {
            Key.Snapshot => HotkeyGesture.PrintScreen,
            >= Key.A and <= Key.Z => ((char)('A' + (key - Key.A))).ToString(),
            >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
            >= Key.F1 and <= Key.F24 => $"F{key - Key.F1 + 1}",
            _ => null,
        };

        return name is null
            ? null
            : new HotkeyGesture(
                modifiers.HasFlag(ModifierKeys.Control),
                modifiers.HasFlag(ModifierKeys.Alt),
                modifiers.HasFlag(ModifierKeys.Shift),
                modifiers.HasFlag(ModifierKeys.Windows),
                name);
    }
}
