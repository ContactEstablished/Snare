namespace Snare.Core.Hotkeys;

/// <summary>
/// Guesses who holds a hotkey. Windows doesn't say which process owns a registration,
/// so this looks at the usual suspects on Matt's machine.
/// </summary>
public static class HotkeyConflictDiagnosis
{
    public static HotkeyConflict Diagnose(
        string gesture,
        bool isPlainPrintScreen,
        IEnumerable<string> runningProcessNames,
        bool snippingToolOwnsPrintScreen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gesture);
        ArgumentNullException.ThrowIfNull(runningProcessNames);

        if (runningProcessNames.Any(name => name.StartsWith("Snagit", StringComparison.OrdinalIgnoreCase)))
        {
            return new HotkeyConflict(gesture, HotkeyOwner.Snagit);
        }

        // Windows' Snipping Tool setting only claims the bare PrtSc key.
        if (isPlainPrintScreen && snippingToolOwnsPrintScreen)
        {
            return new HotkeyConflict(gesture, HotkeyOwner.SnippingTool);
        }

        return new HotkeyConflict(gesture, HotkeyOwner.Unknown);
    }
}
