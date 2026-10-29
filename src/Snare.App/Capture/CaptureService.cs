using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Snare.App.Editor;
using Snare.App.Messages;
using Snare.App.QuickBar;
using Snare.App.Services;
using Snare.Capture;
using Snare.Capture.Selection;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Imaging;
using Snare.Core.Options;
using Snare.Core.Search;
using Snare.Rendering;

namespace Snare.App.Capture;

/// <summary>
/// The still-capture flow: freeze, select, crop, save, copy, quick bar. Runs on the UI thread;
/// cropping and saving (PNG encoding) run on the thread pool.
/// </summary>
public sealed class CaptureService(
    IScreenFreezer freezer,
    IOcrEngine ocr,
    CaptureOverlay overlay,
    ICaptureStore store,
    ClipboardService clipboard,
    EditorService editor,
    TrayIconService tray,
    IOptionsMonitor<SnareOptions> options,
    IServiceProvider services,
    ILogger<CaptureService> logger)
{
    private bool _busy;
    private PixelRect? _lastRegion;
    private QuickBarWindow? _quickBar;

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

            var desktop = Freeze();
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

    /// <summary>Ctrl+Alt+PrtSc: select a region, read its text, put the text on the clipboard. Nothing is saved.</summary>
    public async Task GrabTextAsync()
    {
        if (!TryEnter())
        {
            return;
        }

        try
        {
            var desktop = Freeze();
            var selection = await overlay.SelectAsync(desktop);
            if (selection is null)
            {
                return;
            }

            var image = await Task.Run(() => DesktopCropper.Crop(desktop, selection.Rect));
            var text = (await ocr.RecognizeAsync(image)).Trim();
            if (text.Length == 0)
            {
                tray.Notify("Grab text", "No text found in that area.");
                return;
            }

            var lines = text.Split('\n');
            var copied = await clipboard.CopyTextAsync(text);
            var first = lines[0].Length > 60 ? lines[0][..60] + "…" : lines[0];
            tray.Notify(
                copied ? $"Copied {lines.Length} line{(lines.Length == 1 ? "" : "s")} of text" : "Clipboard busy; text not copied",
                first);
            logger.LogInformation("Grab text read {Lines} lines from {Rect}", lines.Length, selection.Rect);
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
            var desktop = Freeze();
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
        var rect = selection.Rect;
        var center = new PixelPoint(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
        var monitor = (desktop.MonitorAt(center) ?? desktop.MonitorAt(new PixelPoint(rect.X, rect.Y)) ?? desktop.Monitors[0]).Monitor;
        await DeliverAsync(image, source, desktop.CapturedAt, CaptureKind.Image, rect, monitor);
    }

    /// <summary>
    /// After a capture: save it to the library, copy it, then the quick bar (beside
    /// <paramref name="rect"/> on <paramref name="monitor"/>) or the editor. Also used by scrolling capture.
    /// </summary>
    public async Task DeliverAsync(
        PixelBuffer image, CaptureSource source, DateTimeOffset capturedAt, CaptureKind kind, PixelRect rect, MonitorInfo monitor)
    {
        // Save and encode the clipboard PNG side by side. The clipboard gets the pixels even if
        // the save fails, so a broken library never costs Matt the screenshot itself.
        var save = Task.Run(() => store.SaveImageAsync(image, source, capturedAt, kind));
        var png = Task.Run(() => EncodePng(image));
        CaptureRecord? record = null;
        Exception? saveError = null;
        try
        {
            record = await save;
        }
        catch (Exception ex)
        {
            saveError = ex;
        }

        var copied = await clipboard.CopyAsync(image, await png);

        if (record is null)
        {
            logger.LogError(saveError, "Capture {Rect} could not be saved", rect);
            tray.Notify(
                "Capture not saved",
                (copied ? "It is on the clipboard. " : "") + (saveError?.Message ?? "Unknown error."));
            return;
        }

        logger.LogInformation("Captured {Kind} {Width}x{Height} from {Rect} in {App}", kind, image.Width, image.Height, rect, source.App);
        if (!copied)
        {
            tray.Notify("Snare", "Saved, but the clipboard was busy. Use Copy on the quick bar.");
        }

        WeakReferenceMessenger.Default.Send(new CaptureSavedMessage(record));
        if (options.CurrentValue.AfterCapture == AfterCaptureAction.Editor)
        {
            await editor.OpenAsync(record.Id);
        }
        else
        {
            ShowQuickBar(record, rect, monitor);
        }
    }

    /// <summary>Freezes the screen, closing the previous quick bar first so it can't be in the shot.</summary>
    private FrozenDesktop Freeze()
    {
        _quickBar?.Close();
        _quickBar = null;
        return freezer.Freeze(options.CurrentValue.IncludeCursor);
    }

    private byte[]? EncodePng(PixelBuffer image)
    {
        try
        {
            using var bitmap = PixelBufferImages.ToBitmap(image);
            return ImageCodec.EncodePng(bitmap);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not encode PNG clipboard data; copying the bitmap only");
            return null;
        }
    }

    private void ShowQuickBar(CaptureRecord record, PixelRect rect, MonitorInfo monitor)
    {
        var viewModel = ActivatorUtilities.CreateInstance<QuickBarViewModel>(services, record, logger);
        var quickBar = new QuickBarWindow(viewModel, rect, monitor);
        quickBar.Closed += (_, _) =>
        {
            if (ReferenceEquals(_quickBar, quickBar))
            {
                _quickBar = null;
            }
        };
        _quickBar = quickBar;
        quickBar.Show();
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
