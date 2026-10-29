using Microsoft.Win32;

namespace Snare.Interop;

/// <summary>Windows 11's "Use the Print screen key to open screen capture" setting.</summary>
public static class SnippingToolSetting
{
    /// <summary>True when Windows gives PrtSc to Snipping Tool.</summary>
    public static bool IsPrintScreenBoundToSnipping()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Keyboard");
        return IsEnabled(key?.GetValue("PrintScreenKeyForSnippingEnabled"));
    }

    /// <summary>
    /// Reads the registry value. Windows 11 turns the setting on by default, so anything other
    /// than an explicit DWORD 0 (including a missing value) counts as on.
    /// </summary>
    public static bool IsEnabled(object? registryValue) => registryValue is not int value || value != 0;
}
