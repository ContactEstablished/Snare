using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Snare.Interop;

/// <summary>
/// System-wide hotkeys. Registration fails when another app owns the combination;
/// on Windows 11, PrtSc belongs to Snipping Tool until its setting is turned off.
/// </summary>
public static class GlobalHotkeys
{
    /// <summary>Virtual-key code for the Print Screen key.</summary>
    public const uint PrintScreenKey = 0x2C;

    /// <summary>
    /// The virtual-key code for a key name Snare's hotkeys use: PrtSc, A–Z, 0–9 or F1–F24
    /// (canonical spelling, as <c>HotkeyGesture</c> writes it). Null for anything else.
    /// </summary>
    public static uint? VirtualKeyFor(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key == "PrtSc")
        {
            return PrintScreenKey;
        }

        if (key.Length == 1 && (char.IsAsciiLetterUpper(key[0]) || char.IsAsciiDigit(key[0])))
        {
            return key[0]; // VK_A..VK_Z and VK_0..VK_9 are their ASCII codes
        }

        return key.Length is 2 or 3 && key[0] == 'F' && int.TryParse(key.AsSpan(1), out var n) && n is >= 1 and <= 24
            ? (uint)(0x70 + n - 1) // VK_F1 = 0x70
            : null;
    }

    public static bool TryRegister(nint windowHandle, int id, HotkeyModifiers modifiers, uint virtualKey) =>
        PInvoke.RegisterHotKey((HWND)windowHandle, id, (HOT_KEY_MODIFIERS)modifiers, virtualKey);

    public static bool Unregister(nint windowHandle, int id) =>
        PInvoke.UnregisterHotKey((HWND)windowHandle, id);
}
