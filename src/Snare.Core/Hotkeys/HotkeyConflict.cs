namespace Snare.Core.Hotkeys;

/// <summary>A hotkey Snare couldn't register, with who probably holds it and what Matt can do.</summary>
public sealed record HotkeyConflict(string Gesture, HotkeyOwner LikelyOwner)
{
    /// <summary>Short enough for the tray tooltip.</summary>
    public string Summary => LikelyOwner switch
    {
        HotkeyOwner.Snagit => $"{Gesture} is taken by Snagit",
        HotkeyOwner.SnippingTool => $"{Gesture} is taken by Snipping Tool",
        _ => $"{Gesture} is taken by another app",
    };

    public string Advice => LikelyOwner switch
    {
        HotkeyOwner.Snagit =>
            $"Turn off Snagit's {Gesture} hotkey or quit Snagit, then restart Snare, or pick another key in Snare's Settings.",
        HotkeyOwner.SnippingTool =>
            "Turn off Settings > Accessibility > Keyboard > \"Use the Print screen key to open screen capture\", then restart Snare, or pick another key in Snare's Settings.",
        _ => $"Close the app that uses {Gesture}, then restart Snare, or pick another key in Snare's Settings.",
    };
}
