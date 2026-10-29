using Microsoft.Extensions.Logging;
using Snare.Core.Hotkeys;

namespace Snare.App.Services;

/// <summary>Connects the tray menu and global hotkeys to what they start.</summary>
public sealed class ShellService(
    TrayIconService tray,
    HotkeyService hotkeys,
    ILogger<ShellService> logger)
{
    public void Start()
    {
        tray.Show();

        hotkeys.Pressed += OnHotkeyPressed;
        hotkeys.Start();
        tray.ShowHotkeyConflicts(hotkeys.Conflicts);
    }

    private void OnHotkeyPressed(object? sender, HotkeyAction action)
    {
        // Phase 0: the hotkeys only prove they fire. Phase 1 starts real captures here.
        logger.LogInformation("Hotkey action {Action} has no handler yet", action);
    }
}
