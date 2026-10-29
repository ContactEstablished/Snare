using Microsoft.Extensions.Logging;
using Snare.App.Capture;
using Snare.App.Library;
using Snare.App.Recording;
using Snare.App.Scrolling;
using Snare.App.Spikes;
using Snare.Core.Hotkeys;
using Snare.Interop;

namespace Snare.App.Services;

/// <summary>Connects the tray menu and global hotkeys to what they start.</summary>
public sealed class ShellService(
    TrayIconService tray,
    HotkeyService hotkeys,
    CaptureService capture,
    RecordingService recording,
    ScrollCaptureService scrolling,
    LibraryWindowService library,
    FreezeOverlaySpike freezeOverlay,
    SkiaCanvasSpike skiaCanvas,
    RecordingSpikeRunner recordingSpike,
    ILogger<ShellService> logger)
{
    public void Start()
    {
        tray.Show(new TrayMenuActions(
            Capture: () => _ = capture.CaptureAsync(),
            CaptureAfterDelay: seconds => _ = capture.CaptureAsync(seconds),
            GrabText: () => _ = capture.GrabTextAsync(),
            RecordVideo: () => _ = recording.ToggleAsync(),
            ScrollingCapture: () => _ = scrolling.ToggleAsync(),
            OpenLibrary: library.Show));

        // Phase 0 harnesses stay until their manual checks in docs/phases/0-foundation/task.md are ticked.
        tray.AddSpike("Freeze overlay", freezeOverlay.Run);
        tray.AddSpike("Skia canvas (5120×2880 pattern)", skiaCanvas.RunWithTestPattern);
        tray.AddSpike("Record 22 s test (mic, system audio, webcam)", recordingSpike.Run);
        freezeOverlay.CanvasRequested += (_, desktop) => skiaCanvas.RunWithDesktop(desktop);

        WarnIfHdr();

        hotkeys.Pressed += OnHotkeyPressed;
        hotkeys.Start();
        tray.ShowHotkeyConflicts(hotkeys.Conflicts);
    }

    // HDR makes GDI grabs look washed out (build plan: Monitors and DPI). Decided: warn, don't block.
    private void WarnIfHdr()
    {
        var hdr = AdvancedColor.CountHdrDisplays();
        if (hdr > 0)
        {
            logger.LogWarning("{Count} display(s) have HDR on; captures from them may look washed out", hdr);
        }
    }

    private void OnHotkeyPressed(object? sender, HotkeyAction action)
    {
        // CaptureService catches and reports its own failures, so these are fire-and-forget.
        switch (action)
        {
            case HotkeyAction.CaptureImage:
                _ = capture.CaptureAsync();
                break;
            case HotkeyAction.RecordVideo:
                _ = recording.ToggleAsync();
                break;
            case HotkeyAction.CaptureScrolling:
                _ = scrolling.ToggleAsync();
                break;
            case HotkeyAction.GrabText:
                _ = capture.GrabTextAsync();
                break;
            case HotkeyAction.RepeatLastRegion:
                _ = capture.RepeatLastRegionAsync();
                break;
            default:
                logger.LogInformation("Hotkey action {Action} arrives in a later phase", action);
                break;
        }
    }
}
