using Microsoft.Extensions.Logging;
using Snare.App.Spikes;
using Snare.Core.Hotkeys;

namespace Snare.App.Services;

/// <summary>Connects the tray menu and global hotkeys to what they start.</summary>
public sealed class ShellService(
    TrayIconService tray,
    HotkeyService hotkeys,
    FreezeOverlaySpike freezeOverlay,
    SkiaCanvasSpike skiaCanvas,
    RecordingSpikeRunner recordingSpike,
    ILogger<ShellService> logger)
{
    public void Start()
    {
        tray.Show();
        tray.AddSpike("Freeze overlay", freezeOverlay.Run);
        tray.AddSpike("Skia canvas (5120×2880 pattern)", skiaCanvas.RunWithTestPattern);
        tray.AddSpike("Record 22 s test (mic, system audio, webcam)", recordingSpike.Run);
        freezeOverlay.CanvasRequested += (_, desktop) => skiaCanvas.RunWithDesktop(desktop);

        hotkeys.Pressed += OnHotkeyPressed;
        hotkeys.Start();
        tray.ShowHotkeyConflicts(hotkeys.Conflicts);
    }

    private void OnHotkeyPressed(object? sender, HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.CaptureImage:
                // Phase 0: PrtSc opens the freeze overlay spike. Phase 1 starts a real capture.
                freezeOverlay.Run();
                break;
            default:
                logger.LogInformation("Hotkey action {Action} has no handler yet", action);
                break;
        }
    }
}
