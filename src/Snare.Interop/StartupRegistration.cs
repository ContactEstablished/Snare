using Microsoft.Win32;

namespace Snare.Interop;

/// <summary>
/// Start with Windows through the per-user Run key (no admin needed). The value is the quoted
/// path of the exe; tests pass their own key path.
/// </summary>
public sealed class StartupRegistration(string keyPath = StartupRegistration.RunKey, string valueName = "Snare")
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>The command registered now, or null.</summary>
    public string? Current()
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(valueName) as string;
    }

    public bool IsEnabledFor(string exePath) =>
        string.Equals(Current(), Command(exePath), StringComparison.OrdinalIgnoreCase);

    public void Enable(string exePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key.SetValue(valueName, Command(exePath), RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    private static string Command(string exePath) => $"\"{exePath}\"";
}
