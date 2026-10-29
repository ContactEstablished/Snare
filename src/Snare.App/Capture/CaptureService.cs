using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Snare.App.Messages;
using Snare.App.QuickBar;
using Snare.App.Services;
using Snare.Capture;
using Snare.Capture.Selection;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Options;

namespace Snare.App.Capture;

/// <summary>
/// The still-capture flow: freeze, select, crop, save, copy, quick bar. Runs on the UI thread;
/// cropping and saving (PNG encoding) run on the thread pool.
/// </summary>
public sealed class CaptureService(
    IScreenFreezer freezer,
    CaptureOverlay overlay,
    ICaptureStore store,
    ClipboardService clipboard,
    TrayIconService tray,
    IOptionsMonitor<SnareOptions> options,
    IServiceProvider services,
    ILogger<CaptureService> logger)
{
    private bool _busy;
    private PixelRect? _lastRegion;

    /// <summary>PrtSc, or tray → Capture. With a delay, counts down first so menus can be opened.</summary>
    public async Task CaptureAsync(int delaySeconds = 0)
    {
        if (!TryEnter())
        {
            return;
        }

        try
        {
            if (delaySeconds > 0)
            {
                await new CountdownWindow().RunAsync(delaySeconds);
            }

            var desktop = freezer.Freeze(options.CurrentValue.IncludeCursor);
            var selection = await overlay.SelectAsync(desktop);
            if (selection is null)
            {
                logger.LogInformation("Capture cancelled");
                return;
            }

            if (selection.Target == CaptureTarget.Region)
            {
                _lastRegion = selection.Rect;
            }

            await SaveAsync(desktop, selection);
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Ctrl+PrtSc: the previous region again, with no overlay. Falls back to a normal capture.</summary>
    public async Task RepeatLastRegionAsync()
    {
        if (_lastRegion is not { } region)
        {
            await CaptureAsync();
            return;
        }

        if (!TryEnter())
        {
            return;
        }

        try
        {
            var desktop = freezer.Freeze(options.CurrentValue.IncludeCursor);
            var clipped = region.Intersect(desktop.Bounds);
            if (clipped.IsEmpty)
            {
                logger.LogInformation("Last region {Region} is no longer on the desktop", region);
                _lastRegion = null;
                return;
            }

            await SaveAsync(desktop, new SelectionResult(clipped, CaptureTarget.Region));
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task SaveAsync(FrozenDesktop desktop, SelectionResult selection)
    {
        var source = selection.Window is { } window
            ? new CaptureSource(window.ProcessName, window.Title)
            : desktop.SourceUnderCursor();

        var image = await Task.Run(() => DesktopCropper.Crop(desktop, selection.Rect));
        var record = await Task.Run(() => store.SaveImageAsync(image, source, desktop.CapturedAt));
        logger.LogInformation("Captured {Target} {Rect} from {App}", selection.Target, selection.Rect, source.App);

        if (!await clipboard.CopyAsync(image))
        {
            tray.Notify("Snare", "Saved, but the clipboard was busy. Use Copy on the quick bar.");
        }

        WeakReferenceMessenger.Default.Send(new CaptureSavedMessage(record));
        ShowQuickBar(record, selection.Rect, desktop);
    }

    private void ShowQuickBar(CaptureRecord record, PixelRect rect, FrozenDesktop desktop)
    {
        var center = new PixelPoint(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
        var monitor = (desktop.MonitorAt(center) ?? desktop.MonitorAt(new PixelPoint(rect.X, rect.Y)) ?? desktop.Monitors[0]).Monitor;
        var viewModel = ActivatorUtilities.CreateInstance<QuickBarViewModel>(services, record, logger);
        new QuickBarWindow(viewModel, rect, monitor).Show();
    }

    private bool TryEnter()
    {
        if (_busy || overlay.IsOpen)
        {
            return false;
        }

        _busy = true;
        return true;
    }

    private void Fail(Exception ex)
    {
        logger.LogError(ex, "Capture failed");
        tray.Notify("Capture failed", ex.Message);
    }
}
