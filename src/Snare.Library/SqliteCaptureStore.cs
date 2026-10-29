using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Snare.Core.Annotations;
using Snare.Core.Captures;
using Snare.Core.Imaging;
using Snare.Core.Options;
using Snare.Library.Data;
using Snare.Rendering;

namespace Snare.Library;

/// <summary>The library on disk: library.db plus originals, exports and thumbnails.</summary>
public sealed class SqliteCaptureStore : ICaptureStore
{
    private readonly LibraryPaths _paths;
    private readonly IOptionsMonitor<SnareOptions> _options;
    private readonly AnnotationRenderer _renderer;
    private readonly TimeProvider _clock;
    private readonly ILogger<SqliteCaptureStore> _logger;
    private readonly DbContextOptions<SnareDbContext> _dbOptions;
    private readonly SemaphoreSlim _initialize = new(1, 1);
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
        PixelBuffer image, CaptureSource source, DateTimeOffset capturedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(source);
        await EnsureInitializedAsync(cancellationToken);

        var id = Guid.CreateVersion7(capturedAt);
        var options = _options.CurrentValue;
        using var bitmap = PixelBufferImages.ToBitmap(image);

        // 1. Original pixels first: after this, a crash can't lose the capture.
        var originalPath = _paths.OriginalPathFor(id);
        await WriteNewFileAsync(originalPath, ImageCodec.EncodePng(bitmap), cancellationToken);

        // 2. The row, pointing at the export it is about to get.
        var exportPath = _paths.UniqueExportPathFor(capturedAt, options.DefaultExportFormat, File.Exists);
        var now = _clock.GetUtcNow().UtcDateTime;
        var entity = new CaptureEntity
        {
            Id = id,
            Kind = CaptureKind.Image,
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
        }

        // 3. The flattened export. A new capture has no annotations yet, but it goes through
        //    the renderer so the path is the same as a re-edit's.
        var document = new AnnotationDocument { Canvas = new CanvasInfo(image.Width, image.Height) };
        using (var flattened = _renderer.Render(bitmap, document))
        {
            var encoded = options.DefaultExportFormat == ExportFormat.Jpeg
                ? ImageCodec.EncodeJpeg(flattened, options.JpegQuality)
                : ImageCodec.EncodePng(flattened);
            await WriteNewFileAsync(exportPath, encoded, cancellationToken);

            // 4. Thumbnail last; it can always be rebuilt.
            await WriteFileAsync(_paths.ThumbnailPathFor(id), ImageCodec.EncodeThumbnail(flattened), cancellationToken);
        }

        _logger.LogInformation("Saved capture {Id} {Width}x{Height} to {Path}", id, image.Width, image.Height, exportPath);
        return entity.ToRecord();
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
    private static async Task WriteNewFileAsync(string path, byte[] data, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        await File.WriteAllBytesAsync(temp, data, cancellationToken);
        File.Move(temp, path, overwrite: false);
    }

    private static async Task WriteFileAsync(string path, byte[] data, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        await File.WriteAllBytesAsync(temp, data, cancellationToken);
        File.Move(temp, path, overwrite: true);
    }
}
