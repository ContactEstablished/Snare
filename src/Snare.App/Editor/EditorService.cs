using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Snare.App.Services;
using Snare.Core.Annotations;
using Snare.Core.Captures;
using Snare.Rendering;

namespace Snare.App.Editor;

/// <summary>Opens captures in the editor: one window per capture, brought to the front if already open.</summary>
public sealed class EditorService(
    ICaptureStore store,
    TrayIconService tray,
    IServiceProvider services,
    ILogger<EditorService> logger)
{
    private readonly Dictionary<Guid, EditorWindow> _open = [];
    private readonly HashSet<Guid> _opening = [];

    public async Task OpenAsync(Guid captureId)
    {
        if (_open.TryGetValue(captureId, out var existing))
        {
            existing.Activate();
            return;
        }

        // A second click while the first is still loading must not open a second window.
        if (!_opening.Add(captureId))
        {
            return;
        }

        try
        {
            var record = await store.GetAsync(captureId) ?? throw new KeyNotFoundException($"Capture {captureId} is not in the library.");
            if (record.OriginalPath is null)
            {
                tray.Notify("Can't edit this capture", "Its original was discarded to make the redaction permanent.");
                return;
            }

            var pixels = await store.LoadOriginalAsync(captureId);
            var original = await Task.Run(() => PixelBufferImages.ToBitmap(pixels));
            var document = LoadDocument(record, pixels.Width, pixels.Height);

            var viewModel = ActivatorUtilities.CreateInstance<EditorViewModel>(
                services, record, original, document, (ILogger)logger);
            var window = new EditorWindow(viewModel);
            _open[captureId] = window;
            window.Closed += (_, _) => _open.Remove(captureId);
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not open capture {Id} in the editor", captureId);
            tray.Notify("Can't open the editor", ex.Message);
        }
        finally
        {
            _opening.Remove(captureId);
        }
    }

    /// <summary>
    /// The saved annotation layer, or a fresh one for a capture never edited.
    /// </summary>
    private AnnotationDocument LoadDocument(CaptureRecord record, int width, int height)
    {
        if (record.AnnotationJson is { Length: > 0 } json)
        {
            try
            {
                return AnnotationJson.Deserialize(json);
            }
            catch (System.Text.Json.JsonException ex)
            {
                logger.LogWarning(ex, "Ignoring unreadable annotations on capture {Id}", record.Id);
            }
        }

        return new AnnotationDocument { Canvas = new CanvasInfo(width, height) };
    }
}
