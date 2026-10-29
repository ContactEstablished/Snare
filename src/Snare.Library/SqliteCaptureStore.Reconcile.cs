using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snare.Core.Annotations;
using Snare.Core.Captures;
using Snare.Library.Data;
using Snare.Rendering;

namespace Snare.Library;

/// <summary>Startup repair after files were added, moved or deleted outside the app.</summary>
public sealed partial class SqliteCaptureStore
{
    /// <summary>Most imports in one start, so a huge folder dropped into the library can't stall it.</summary>
    public const int ImportLimit = 5000;

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg"];

    /// <summary>
    /// Re-exports images whose export is gone but whose original remains; moves captures with
    /// neither to the Trash; imports images under the library root that no capture knows about.
    /// </summary>
    public async Task<ReconcileResult> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var reexported = 0;
        var trashed = 0;

        List<CaptureEntity> rows;
        await using (var db = new SnareDbContext(_dbOptions))
        {
            rows = await db.Captures.AsNoTracking().ToListAsync(cancellationToken);
        }

        foreach (var row in rows.Where(r => !r.IsDeleted && r.Kind != CaptureKind.Video && !File.Exists(r.FilePath)))
        {
            if (row.OriginalPath is { } original && File.Exists(original))
            {
                try
                {
                    await ReexportAsync(row.Id, DocumentFor(row), cancellationToken);
                    reexported++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One bad capture must not stop the rest of startup repair.
                    _logger.LogWarning(ex, "Could not re-export missing capture {Id} to {Path}", row.Id, row.FilePath);
                }
            }
            else
            {
                await MoveToTrashAsync(row.Id, cancellationToken);
                _logger.LogWarning("Capture {Id} is missing on disk ({Path}); moved to the Trash", row.Id, row.FilePath);
                trashed++;
            }
        }

        var imported = await ImportUnknownFilesAsync(rows, cancellationToken);
        var result = new ReconcileResult(reexported, trashed, imported);
        if (result.Changed)
        {
            _logger.LogInformation("Library reconciled: {Reexported} re-exported, {Trashed} trashed, {Imported} imported",
                reexported, trashed, imported);
        }

        return result;
    }

    private async Task<int> ImportUnknownFilesAsync(List<CaptureEntity> rows, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_paths.LibraryRoot))
        {
            return 0;
        }

        var known = new HashSet<string>(rows.Select(r => Path.GetFullPath(r.FilePath)), StringComparer.OrdinalIgnoreCase);
        // Skip folders Snare can't read instead of failing the whole scan.
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        var candidates = Directory
            .EnumerateFiles(_paths.LibraryRoot, "*", options)
            .Where(p => ImageExtensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
            .Where(p => !known.Contains(Path.GetFullPath(p)));

        var imported = 0;
        foreach (var path in candidates)
        {
            if (imported >= ImportLimit)
            {
                _logger.LogWarning("Stopped importing after {Limit} files; the rest will be imported on the next start", ImportLimit);
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (await ImportAsync(path, cancellationToken))
                {
                    imported++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Skipping {Path}: it could not be imported", path);
            }
        }

        return imported;
    }

    /// <summary>Adds an outside image as a capture. The file stays where it is; a copy becomes the editable original.</summary>
    private async Task<bool> ImportAsync(string path, CancellationToken cancellationToken)
    {
        SkiaSharp.SKBitmap bitmap;
        try
        {
            bitmap = ImageCodec.Decode(await File.ReadAllBytesAsync(path, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Skipping {Path}: it isn't a readable image", path);
            return false;
        }

        // Version 7 GUIDs can't encode times before 1970 (some copied files claim 1601).
        var created = File.GetLastWriteTimeUtc(path);
        if (created < DateTime.UnixEpoch)
        {
            created = DateTime.UnixEpoch;
        }

        var id = Guid.CreateVersion7(new DateTimeOffset(created, TimeSpan.Zero));
        var fullPath = Path.GetFullPath(path);
        using (bitmap)
        using (await WriteGateAsync(cancellationToken))
        {
            // A capture saved while the scan ran may already own this file.
            await using (var check = new SnareDbContext(_dbOptions))
            {
                if (await check.Captures.AnyAsync(c => c.FilePath == fullPath, cancellationToken))
                {
                    return false;
                }
            }

            var originalPath = _paths.OriginalPathFor(id);
            await WriteNewFileAsync(originalPath, ImageCodec.EncodePng(bitmap), cancellationToken);
            await WriteFileAsync(_paths.ThumbnailPathFor(id), ImageCodec.EncodeThumbnail(bitmap), cancellationToken);

            await using var db = new SnareDbContext(_dbOptions);
            db.Captures.Add(new CaptureEntity
            {
                Id = id,
                Kind = CaptureKind.Image,
                FilePath = fullPath,
                OriginalPath = originalPath,
                Width = bitmap.Width,
                Height = bitmap.Height,
                CreatedUtc = created,
                ModifiedUtc = _clock.GetUtcNow().UtcDateTime,
                SourceApp = "imported",
                Title = Path.GetFileNameWithoutExtension(path),
            });
            await SaveAndReindexAsync(db, [id], cancellationToken);
        }

        _logger.LogInformation("Imported {Path}", path);
        ImageChanged?.Invoke(this, id);
        return true;
    }

    private static AnnotationDocument DocumentFor(CaptureEntity row)
    {
        if (row.AnnotationJson is { Length: > 0 } json)
        {
            try
            {
                return AnnotationJson.Deserialize(json);
            }
            catch (System.Text.Json.JsonException)
            {
                // Fall through: re-export the plain original rather than nothing.
            }
        }

        return new AnnotationDocument { Canvas = new CanvasInfo(row.Width, row.Height) };
    }
}
