using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Snare.App.Services;
using Snare.Core.Options;
using Snare.Interop;
using Snare.Library;

namespace Snare.App.Settings;

/// <summary>
/// Opens Settings and applies what it saves: settings.json (which reloads the options, and with
/// them the hotkeys and library folder), Start with Windows, and one-click backups.
/// </summary>
public sealed class SettingsService(
    UserSettingsStore store,
    IOptionsMonitor<SnareOptions> options,
    HotkeyService hotkeys,
    LibraryBackup backup,
    TrayIconService tray,
    ILogger<SettingsService> logger)
{
    private SettingsWindow? _window;
    private HotkeyOptions? _savedHotkeys;

    /// <summary>Where tools/publish.ps1 installs Snare. Only that copy registers itself to start with Windows.</summary>
    public static string InstallFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Snare");

    /// <summary>Tray → Settings…: one window at a time; hotkeys pause while it's open.</summary>
    public void Show()
    {
        if (_window is not null)
        {
            _window.Activate();
            return;
        }

        var viewModel = new SettingsViewModel(
            options.CurrentValue, SaveAsync, BackUpAsync, backup.LastBackupTime, LibraryPaths.DefaultLibraryRoot, LibraryBackup.DefaultFolder);
        _window = new SettingsWindow(viewModel);
        _savedHotkeys = null;
        _window.Closed += (_, _) =>
        {
            _window = null;
            // The saved keys, not options.CurrentValue: settings.json reloads a moment later.
            hotkeys.Resume(_savedHotkeys);
        };
        hotkeys.Suspend();
        _window.Show();
        _window.Activate();
    }

    /// <summary>
    /// Makes the Run key match the setting. Only the installed copy (see <see cref="InstallFolder"/>)
    /// writes it, so a build run from a worktree never ends up in Matt's sign-in.
    /// </summary>
    public void ApplyStartWithWindows(bool enabled)
    {
        if (Environment.ProcessPath is not { } running || !IsInstalledCopy(running, InstallFolder))
        {
            logger.LogInformation("Not the installed copy: leaving Start with Windows alone (setting is {Enabled})", enabled);
            return;
        }

        try
        {
            var startup = new StartupRegistration();
            if (!enabled)
            {
                startup.Disable();
            }
            else if (Environment.ProcessPath is { } exe && !startup.IsEnabledFor(exe))
            {
                startup.Enable(exe);
                logger.LogInformation("Snare starts with Windows from {Path}", exe);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            logger.LogWarning(ex, "Could not update Start with Windows");
        }
    }

    public static bool IsInstalledCopy(string exePath, string installFolder) =>
        string.Equals(Path.GetDirectoryName(Path.GetFullPath(exePath)), Path.GetFullPath(installFolder).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    /// <summary>Tray → Back up library, and Settings → Back up now. Returns the message shown.</summary>
    public async Task<string> BackUpAsync()
    {
        string message;
        try
        {
            var file = await backup.CreateAsync();
            message = string.Create(CultureInfo.CurrentCulture, $"Saved {file.Name} ({file.Length / 1024.0 / 1024.0:N1} MB) in {file.DirectoryName}.");
            tray.Notify("Library backed up", message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Backup failed");
            message = $"Backup failed: {ex.Message}";
            tray.Notify("Backup failed", ex.Message);
        }

        return message;
    }

    private async Task SaveAsync(SnareOptions edited)
    {
        await store.SaveAsync(edited);
        _savedHotkeys = edited.Hotkeys;
        ApplyStartWithWindows(edited.StartWithWindows);
        logger.LogInformation("Settings saved to {Path}", store.FilePath);
    }
}
