using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Snare.Core.Annotations;
using Snare.Core.Captures;
using Snare.Core.Imaging;
using Snare.Core.Options;
using Snare.Core.Search;
using Snare.Library.Data;
using Snare.Rendering;

namespace Snare.Library;

/// <summary>The library on disk: library.db plus originals, exports and thumbnails.</summary>
public sealed partial class SqliteCaptureStore : ICaptureStore, ICaptureCatalog
{
    private readonly LibraryPaths _paths;
    private readonly IOptionsMonitor<SnareOptions> _options;
    private readonly AnnotationRenderer _renderer;
    private readonly TimeProvider _clock;
    private readonly ILogger<SqliteCaptureStore> _logger;
    private readonly DbContextOptions<SnareDbContext> _dbOptions;
    private readonly SemaphoreSlim _initialize = new(1, 1);
    private readonly SemaphoreSlim _save = new(1, 1);
    private bool _initialized;

    public SqliteCaptureStore(
        LibraryPaths paths,
        IOptionsMonitor<SnareOptions> options,
        AnnotationRenderer renderer,
        TimeProvider clock,
        ILogger<SqliteCaptureStore> logger)
    {
        _paths = paths;
        _options = options;
        _renderer = renderer;
        _clock = clock;
        _logger = logger;
        _dbOptions = new DbContextOptionsBuilder<SnareDbContext>()
            .UseSqlite(ConnectionString)
            .Options;
    }

    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath }.ToString();

    public async Task<CaptureRecord> SaveImageAsync(
        PixelBuffer image, CaptureSource source, DateTimeOffset capturedAt, CaptureKind kind = CaptureKind.Image,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(source);

        var id = Guid.CreateVersion7(capturedAt);
        var options = _options.CurrentValue;
        using var bitmap = PixelBufferImages.ToBitmap(image);

        // 1. Original pixels first, before anything touches the database: after this, even a
        //    locked or broken library.db can't lose the capture.
        var originalPath = _paths.OriginalPathFor(id);
        await WriteNewFileAsync(originalPath, ImageCodec.EncodePng(bitmap), cancellationToken);

        await EnsureInitializedAsync(cancellationToken);

        // One save at a time, so two captures in the same second can't pick the same export name.
        await _save.WaitAsync(cancellationToken);
        try
        {
            return await SaveRowAndExportAsync(id, kind, bitmap, image, source, capturedAt, originalPath, options, cancellationToken);
        }
        finally
        {
            _save.Release();
        }
    }

    private async Task<CaptureRecord> SaveRowAndExportAsync(
        Guid id,
        CaptureKind kind,
        SKBitmap bitmap,
        PixelBuffer image,
        CaptureSource source,
        DateTimeOffset capturedAt,
        string originalPath,
        SnareOptions options,
        CancellationToken cancellationToken)
    {
        // 2. The row, pointing at the export it is about to get.
        var exportPath = _paths.UniqueExportPathFor(capturedAt, options.DefaultExportFormat, File.Exists);
        var now = _clock.GetUtcNow().UtcDateTime;
        var entity = new CaptureEntity
        {
            Id = id,
            Kind = kind,
            FilePath = exportPath,
            OriginalPath = originalPath,
            Width = image.Width,
            Height = image.Height,
            CreatedUtc = capturedAt.UtcDateTime,
            ModifiedUtc = now,
            SourceApp = source.App,
            SourceWindowTitle = source.WindowTitle,
        };
        await using (var db = new SnareDbContext(_dbOptions))
        {
            db.Captures.Add(entity);
            await db.SaveChangesAsync(cancellationToken);
            await ReindexAsync(db, id, cancellationToken);
        }

        // 3. The flattened export. A new capture has no annotations yet, but it goes through
        //    the renderer so the path is the same as a re-edit's.
        var document = new AnnotationDocument { Canvas = new CanvasInfo(image.Width, image.Height) };
        using (var flattened = _renderer.Render(bitmap, document))
        {
            // The thumbnail goes first so the library tile shows even if the export below fails
            // (for example, Pictures blocked by Controlled Folder Access, or a full disk). The
            // original and row are already safe; Phase 3's startup reconciliation re-exports.
            await WriteFileAsync(_paths.ThumbnailPathFor(id), ImageCodec.EncodeThumbnail(flattened), cancellationToken);

            var encoded = options.DefaultExportFormat == ExportFormat.Jpeg
                ? ImageCodec.EncodeJpeg(flattened, options.JpegQuality)
                : ImageCodec.EncodePng(flattened);
            try
            {
                await WriteNewFileAsync(exportPath, encoded, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Capture {Id} is in the library, but its export to {Path} failed", id, exportPath);
                throw;
            }
        }

        _logger.LogInformation("Saved capture {Id} {Width}x{Height} to {Path}", id, image.Width, image.Height, exportPath);
        ImageChanged?.Invoke(this, id);
        return entity.ToRecord();
    }

    public async Task<PixelBuffer> LoadOriginalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await GetAsync(id, cancellationToken) ?? throw new KeyNotFoundException($"No capture {id}.");
        using var bitmap = await LoadSourceBitmapAsync(record, cancellationToken);
        var pixels = new byte[PixelBuffer.ByteCount(bitmap.Width, bitmap.Height)];
        System.Runtime.InteropServices.Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        return new PixelBuffer(bitmap.Width, bitmap.Height, pixels);
    }

    public async Task SaveAnnotationsAsync(Guid id, AnnotationDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        await EnsureInitializedAsync(cancellationToken);

        // Serialized with re-export and discard, so an autosave can't land after them.
        await _save.WaitAsync(cancellationToken);
        try
        {
            await using var db = new SnareDbContext(_dbOptions);
            var row = await db.Captures.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException($"No capture {id}.");
            if (row.OriginalPath is null)
            {
                return; // discarded: there is no editable layer any more
            }

            row.AnnotationJson = AnnotationJson.Serialize(document);
            row.ModifiedUtc = _clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _save.Release();
        }
    }

    public async Task<CaptureRecord> ReexportAsync(Guid id, AnnotationDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        await EnsureInitializedAsync(cancellationToken);
        await _save.WaitAsync(cancellationToken);
        try
        {
            var updated = await ReexportCoreAsync(id, document, discardOriginal: false, cancellationToken);
            ImageChanged?.Invoke(this, id);
            return updated;
        }
        finally
        {
            _save.Release();
        }
    }

    public async Task<CaptureRecord> DiscardOriginalAsync(Guid id, AnnotationDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        await EnsureInitializedAsync(cancellationToken);
        await _save.WaitAsync(cancellationToken);
        try
        {
            var updated = await ReexportCoreAsync(id, document, discardOriginal: true, cancellationToken);
            ImageChanged?.Invoke(this, id);
            return updated;
        }
        finally
        {
            _save.Release();
        }
    }

    /// <summary>
    /// Export first, then the row, then (when discarding) the original: if anything fails,
    /// the original is still there to try again.
    /// </summary>
    private async Task<CaptureRecord> ReexportCoreAsync(Guid id, AnnotationDocument document, bool discardOriginal, CancellationToken cancellationToken)
    {
        await using var db = new SnareDbContext(_dbOptions);
        var row = await db.Captures.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"No capture {id}.");
        if (row.OriginalPath is null)
        {
            throw new InvalidOperationException($"Capture {id} has no original; it can't be re-edited.");
        }

        var originalPath = row.OriginalPath;

        // A discard that deleted the original but failed to update the row already wrote the
        // redacted export; finishing it needs only the row.
        var alreadyDeleted = discardOriginal && !File.Exists(originalPath);
        if (!alreadyDeleted)
        {
            var options = _options.CurrentValue;
            using var original = await LoadSourceBitmapAsync(row.ToRecord(), cancellationToken);
            using var flattened = _renderer.Render(original, document);
            var encoded = ExportEncoder.Encode(flattened, ExportEncoder.FormatOf(row.FilePath), options.JpegQuality);
            await WriteFileAsync(row.FilePath, encoded, cancellationToken);
            await WriteFileAsync(_paths.ThumbnailPathFor(id), ImageCodec.EncodeThumbnail(flattened), cancellationToken);
            row.Width = flattened.Width;
            row.Height = flattened.Height;
        }

        if (discardOriginal)
        {
            // Delete before the row says "discarded": if the delete fails (a lock), the row still
            // points at the original and the discard can be retried.
            File.Delete(originalPath);
        }

        row.AnnotationJson = discardOriginal ? null : AnnotationJson.Serialize(document);
        row.OriginalPath = discardOriginal ? null : row.OriginalPath;
        row.ModifiedUtc = _clock.GetUtcNow().UtcDateTime;

        // The image changed (perhaps a password was just redacted): forget the old text until it is read again.
        row.OcrText = null;
        await SaveAndReindexAsync(db, [id], cancellationToken);

        _logger.LogInformation(
            discardOriginal ? "Discarded the original of capture {Id}; {Path} is now its only copy" : "Re-exported capture {Id} to {Path}",
            id, row.FilePath);
        return row.ToRecord();
    }

    private static async Task<SKBitmap> LoadSourceBitmapAsync(CaptureRecord record, CancellationToken cancellationToken)
    {
        // A missing original must not quietly fall back to the export: annotations and crop
        // would then be applied a second time on every save.
        if (record.OriginalPath is { } original && !File.Exists(original))
        {
            throw new FileNotFoundException($"The original of capture {record.Id} is missing.", original);
        }

        var path = record.OriginalPath ?? record.FilePath;
        return ImageCodec.Decode(await File.ReadAllBytesAsync(path, cancellationToken));
    }

    public string NewVideoPath(DateTimeOffset capturedAt) => _paths.UniqueVideoPathFor(capturedAt, File.Exists);

    public async Task<CaptureRecord> SaveVideoAsync(
        string path, int width, int height, long durationMs, CaptureSource source, DateTimeOffset capturedAt, byte[]? thumbnailPng,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(source);
        await EnsureInitializedAsync(cancellationToken);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The recording is missing.", path);
        }

        var id = Guid.CreateVersion7(capturedAt);
        await WriteVideoThumbnailAsync(id, thumbnailPng, cancellationToken);

        using var gate = await WriteGateAsync(cancellationToken);
        await using var db = new SnareDbContext(_dbOptions);
        var entity = new CaptureEntity
        {
            Id = id,
            Kind = CaptureKind.Video,
            FilePath = Path.GetFullPath(path),
            Width = width,
            Height = height,
            DurationMs = durationMs,
            CreatedUtc = capturedAt.UtcDateTime,
            ModifiedUtc = _clock.GetUtcNow().UtcDateTime,
            SourceApp = source.App,
            SourceWindowTitle = source.WindowTitle,
            OcrText = "", // videos aren't OCR'd
        };
        db.Captures.Add(entity);
        await SaveAndReindexAsync(db, [id], cancellationToken);
        _logger.LogInformation("Saved recording {Id} ({Duration} ms) at {Path}", id, durationMs, path);
        return entity.ToRecord();
    }

    public async Task<CaptureRecord> UpdateVideoAsync(Guid id, long durationMs, byte[]? thumbnailPng, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await WriteVideoThumbnailAsync(id, thumbnailPng, cancellationToken);
        using var gate = await WriteGateAsync(cancellationToken);
        await using var db = new SnareDbContext(_dbOptions);
        var row = await RequireAsync(db, id, cancellationToken);
        row.DurationMs = durationMs;
        row.ModifiedUtc = _clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return row.ToRecord();
    }

    /// <summary>A video's thumbnail comes from FFmpeg as PNG; the library keeps WebP like every other.</summary>
    private async Task WriteVideoThumbnailAsync(Guid id, byte[]? png, CancellationToken cancellationToken)
    {
        if (png is null)
        {
            return;
        }

        try
        {
            using var bitmap = ImageCodec.Decode(png);
            await WriteFileAsync(_paths.ThumbnailPathFor(id), ImageCodec.EncodeThumbnail(bitmap), cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Ignoring an unreadable thumbnail for recording {Id}", id);
        }
    }

    public async Task<IReadOnlyList<CaptureRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = new SnareDbContext(_dbOptions);
        var rows = await db.Captures.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderByDescending(c => c.CreatedUtc)
            .ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(r => r.ToRecord()).ToList();
    }

    public async Task<CaptureRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = new SnareDbContext(_dbOptions);
        var row = await db.Captures.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return row?.ToRecord();
    }

    public async Task MoveToTrashAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        using var gate = await WriteGateAsync(cancellationToken);
        await using var db = new SnareDbContext(_dbOptions);
        var row = await db.Captures.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (row is null || row.IsDeleted)
        {
            return;
        }

        row.IsDeleted = true;
        row.ModifiedUtc = _clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Moved capture {Id} to the Trash", id);
    }

    public string ThumbnailPathFor(Guid id) => _paths.ThumbnailPathFor(id);

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initialize.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            Directory.CreateDirectory(_paths.AppDataRoot);
            Directory.CreateDirectory(_paths.OriginalsFolder);
            Directory.CreateDirectory(_paths.ThumbnailsFolder);
            await using (var connection = new SqliteConnection(ConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await SchemaMigrator.MigrateAsync(connection, cancellationToken);
            }

            _initialized = true;
        }
        finally
        {
            _initialize.Release();
        }
    }

    /// <summary>Writes to a temp file, then moves it into place without overwriting anything.</summary>
    private static Task WriteNewFileAsync(string path, byte[] data, CancellationToken cancellationToken) =>
        WriteViaTempAsync(path, data, overwrite: false, cancellationToken);

    private static Task WriteFileAsync(string path, byte[] data, CancellationToken cancellationToken) =>
        WriteViaTempAsync(path, data, overwrite: true, cancellationToken);

    /// <summary>A unique temp name next to the target, removed again if anything fails.</summary>
    private static async Task WriteViaTempAsync(string path, byte[] data, bool overwrite, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.{Path.GetRandomFileName()}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temp, data, cancellationToken);
            File.Move(temp, path, overwrite);
        }
        finally
        {
            File.Delete(temp); // no-op once moved
        }
    }
}
