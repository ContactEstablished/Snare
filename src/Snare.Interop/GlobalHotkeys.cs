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

    public static bool TryRegister(nint windowHandle, int id, HotkeyModifiers modifiers, uint virtualKey) =>
        PInvoke.RegisterHotKey((HWND)windowHandle, id, (HOT_KEY_MODIFIERS)modifiers, virtualKey);

    public static bool Unregister(nint windowHandle, int id) =>
        PInvoke.UnregisterHotKey((HWND)windowHandle, id);
}
