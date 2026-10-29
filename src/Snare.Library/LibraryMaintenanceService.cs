using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snare.Core.Captures;
using Snare.Core.Imaging;
using Snare.Core.Search;
using Snare.Library.Ocr;
using Snare.Rendering;

namespace Snare.Library;

/// <summary>
/// Background library work, off the UI thread: reconcile files at startup, then read the text
/// of every image that hasn't been read, and of each new or re-exported one as it arrives.
/// </summary>
public sealed class LibraryMaintenanceService : BackgroundService
{
    private readonly SqliteCaptureStore _store;
    private readonly IOcrEngine _ocr;
    private readonly OcrQueue _queue;
    private readonly ILogger<LibraryMaintenanceService> _logger;

    public LibraryMaintenanceService(SqliteCaptureStore store, IOcrEngine ocr, OcrQueue queue, ILogger<LibraryMaintenanceService> logger)
    {
        _store = store;
        _ocr = ocr;
        _queue = queue;
        _logger = logger;

        // Subscribe now, not in ExecuteAsync, so captures saved during startup aren't missed.
        _store.ImageChanged += (_, id) => _queue.Enqueue(id);
    }

    /// <summary>Signals that startup reconciliation and OCR backfill have been queued.</summary>
    public Task Started => _started.Task;

    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the tray come up first.
        await Task.Yield();
        try
        {
            await _store.ReconcileAsync(stoppingToken);
            foreach (var id in await _store.ListAwaitingOcrAsync(stoppingToken))
            {
                _queue.Enqueue(id);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Library startup maintenance failed");
        }
        finally
        {
            _started.TrySetResult();
        }

        await foreach (var id in _queue.ReadAllAsync(stoppingToken))
        {
            await ReadTextAsync(id, stoppingToken);
        }
    }

    /// <summary>OCRs the export (what Matt sees, annotations included). One bad image never stops the queue.</summary>
    private async Task ReadTextAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var record = await _store.GetAsync(id, cancellationToken);
            if (record is null || record.IsDeleted || record.Kind == CaptureKind.Video || !File.Exists(record.FilePath))
            {
                return;
            }

            var bytes = await File.ReadAllBytesAsync(record.FilePath, cancellationToken);
            var pixels = await Task.Run(() =>
            {
                using var bitmap = ImageCodec.Decode(bytes);
                var buffer = new byte[PixelBuffer.ByteCount(bitmap.Width, bitmap.Height)];
                System.Runtime.InteropServices.Marshal.Copy(bitmap.GetPixels(), buffer, 0, buffer.Length);
                return new PixelBuffer(bitmap.Width, bitmap.Height, buffer);
            }, cancellationToken);

            var text = await _ocr.RecognizeAsync(pixels, cancellationToken);
            await _store.SetOcrTextAsync(id, text, cancellationToken);
            _logger.LogDebug("Read {Length} characters of text from capture {Id}", text.Length, id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the text of capture {Id}", id);
        }
    }
}
