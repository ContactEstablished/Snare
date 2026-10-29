using Microsoft.Win32;

namespace Snare.Interop;

/// <summary>Windows 11's "Use the Print screen key to open screen capture" setting.</summary>
public static class SnippingToolSetting
{
    /// <summary>
    /// True when Windows gives PrtSc to Snipping Tool. Windows 11 turns this on by default,
    /// so a missing value counts as on.
    /// </summary>
    public static bool IsPrintScreenBoundToSnipping()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Keyboard");
        return key?.GetValue("PrintScreenKeyForSnippingEnabled") is not int value || value != 0;
    }
}
