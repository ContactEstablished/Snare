using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snare.Core.Captures;
using Snare.Core.Search;
using Snare.Library.Data;

namespace Snare.Library;

/// <summary>
/// The catalog half of the store: details, tags, OCR text, search and the Trash. Every change to
/// searchable text rewrites the capture's CaptureSearch (FTS5) row.
/// </summary>
public sealed partial class SqliteCaptureStore
{
    // bm25 weights per CaptureSearch column: CaptureId, Title, Description, Tags, OcrText, SourceApp, SourceWindowTitle.
    private const string Bm25 = "bm25(CaptureSearch, 0.0, 10.0, 5.0, 8.0, 1.0, 3.0, 2.0)";

    public event EventHandler<Guid>? ImageChanged;

    public async Task<IReadOnlyList<CaptureRecord>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await EnsureInitializedAsync(cancellationToken);

        var sql = new System.Text.StringBuilder("SELECT c.* FROM Capture c ");
        var parameters = new List<SqliteParameter>();
        var fts = query.ToFtsExpression();
        if (fts is not null)
        {
            sql.Append("JOIN CaptureSearch ON CaptureSearch.CaptureId = c.Id ");
        }

        sql.Append("WHERE c.IsDeleted = 0 ");
        if (fts is not null)
        {
            sql.Append("AND CaptureSearch MATCH $fts ");
            parameters.Add(new SqliteParameter("$fts", fts));
        }

        if (query.Kind is { } kind)
        {
            sql.Append("AND c.Kind = $kind ");
            parameters.Add(new SqliteParameter("$kind", kind.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(query.App))
        {
            sql.Append("AND c.SourceApp LIKE $app ESCAPE '\\' ");
            parameters.Add(new SqliteParameter("$app", EscapeLike(query.App) + "%"));
        }

        if (query.Before is { } before)
        {
            sql.Append("AND c.CreatedUtc < $before ");
            parameters.Add(new SqliteParameter("$before", StartOfLocalDayUtc(before)));
        }

        if (query.After is { } after)
        {
            sql.Append("AND c.CreatedUtc >= $after ");
            parameters.Add(new SqliteParameter("$after", StartOfLocalDayUtc(after)));
        }

        for (var i = 0; i < query.Tags.Count; i++)
        {
            sql.Append(CultureInfo.InvariantCulture,
                $"AND EXISTS (SELECT 1 FROM CaptureTag ct JOIN Tag t ON t.Id = ct.TagId WHERE ct.CaptureId = c.Id AND t.Name = $tag{i}) ");
            parameters.Add(new SqliteParameter($"$tag{i}", query.Tags[i]));
        }

        sql.Append(fts is not null ? $"ORDER BY {Bm25}, c.CreatedUtc DESC" : "ORDER BY c.CreatedUtc DESC, c.Id DESC");

        await using var db = new SnareDbContext(_dbOptions);
        try
        {
            var rows = await db.Captures.FromSqlRaw(sql.ToString(), [.. parameters]).AsNoTracking().ToListAsync(cancellationToken);
            return rows.Select(r => r.ToRecord()).ToList();
        }
        catch (SqliteException ex) when (fts is not null)
        {
            // A term FTS5 can't parse (rare after quoting) is no match rather than an error.
            _logger.LogDebug(ex, "Search expression {Fts} was rejected", fts);
            return [];
        }
    }

    public async Task<CaptureRecord> UpdateDetailsAsync(Guid id, string? title, string? description, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = new SnareDbContext(_dbOptions);
        var row = await RequireAsync(db, id, cancellationToken);
        row.Title = Blank(title);
        row.Description = Blank(description);
        row.ModifiedUtc = _clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        await ReindexAsync(db, id, cancellationToken);
        return row.ToRecord();
    }

    public async Task<IReadOnlyList<string>> GetTagsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = new SnareDbContext(_dbOptions);
        return await (from ct in db.CaptureTags
                      join t in db.Tags on ct.TagId equals t.Id
                      where ct.CaptureId == id
                      orderby t.Name
                      select t.Name).ToListAsync(cancellationToken);
    }

    public async Task SetTagsAsync(Guid id, IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(names);
        await EnsureInitializedAsync(cancellationToken);
        await using var db = new SnareDbContext(_dbOptions);
        await RequireAsync(db, id, cancellationToken);

        var tags = await ResolveTagsAsync(db, names, cancellationToken);
        db.CaptureTags.RemoveRange(await db.CaptureTags.Where(ct => ct.CaptureId == id).ToListAsync(cancellationToken));
        db.CaptureTags.AddRange(tags.Select(t => new CaptureTagEntity { CaptureId = id, TagId = t.Id }));
        await db.SaveChangesAsync(cancellationToken);

        await RemoveUnusedTagsAsync(db, cancellationToken);
        await ReindexAsync(db, id, cancellationToken);
    }

    public async Task AddTagsAsync(IEnumerable<Guid> ids, IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(names);
        await EnsureInitializedAsync(cancellationToken);
        await using var db = new SnareDbContext(_dbOptions);

        var tags = await ResolveTagsAsync(db, names, cancellationToken);
        var targets = ids.Distinct().ToList();
        foreach (var id in targets)
        {
            var existing = await db.CaptureTags.Where(ct => ct.CaptureId == id).Select(ct => ct.TagId).ToListAsync(cancellationToken);
            db.CaptureTags.AddRange(tags.Where(t => !existing.Contains(t.Id)).Select(t => new CaptureTagEntity { CaptureId = id, TagId = t.Id }));
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var id in targets)
        {
            await ReindexAsync(db, id, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<TagInfo>> ListTagsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = new SnareDbContext(_dbOptions);
        var counts = await (from ct in db.CaptureTags
                            join c in db.Captures on ct.CaptureId equals c.Id
                            where !c.IsDeleted
                            group ct by ct.TagId into g
                            select new { TagId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.TagId, x => x.Count, cancellationToken);
        var tags = await db.Tags.AsNoTracking().ToListAsync(cancellationToken);
        return tags
            .Select(t => new TagInfo(t.Name, t.Color ?? TagPalette.ForIndex(t.Id), counts.GetValueOrDefault(t.Id)))
            .OrderByDescending(t => t.Count)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task SetTagColorAsync(string name, string color, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(color);
        await EnsureInitializedAsync(cancellationToken);
        await using var db = new SnareDbContext(_dbOptions);
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Name == name.Trim(), cancellationToken)
            ?? throw new KeyNotFoundException($"No tag '{name}'.");
        tag.Color = color;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetOcrTextAsync(Guid id, string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        await EnsureInitializedAsync(cancellationToken);
        await using var db = new SnareDbContext(_dbOptions);
        var row = await db.Captures.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (row is null)
        {
            return; // emptied from the Trash while it was being read
        }

        row.OcrText = text;
        await db.SaveChangesAsync(cancellationToken);
        await ReindexAsync(db, id, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListAwaitingOcrAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = new SnareDbContext(_dbOptions);
        return await db.Captures
            .Where(c => !c.IsDeleted && c.OcrText == null && c.Kind != CaptureKind.Video)
            .OrderByDescending(c => c.CreatedUtc)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CaptureRecord>> ListTrashAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = new SnareDbContext(_dbOptions);
        var rows = await db.Captures.AsNoTracking()
            .Where(c => c.IsDeleted)
            .OrderByDescending(c => c.ModifiedUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(r => r.ToRecord()).ToList();
    }

    public async Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = new SnareDbContext(_dbOptions);
        var row = await RequireAsync(db, id, cancellationToken);
        row.IsDeleted = false;
        row.ModifiedUtc = _clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> EmptyTrashAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _save.WaitAsync(cancellationToken);
        try
        {
            await using var db = new SnareDbContext(_dbOptions);
            var trashed = await db.Captures.Where(c => c.IsDeleted).ToListAsync(cancellationToken);
            var removed = 0;
            foreach (var row in trashed)
            {
                try
                {
                    // Files first: a row is only removed once nothing of it is left on disk.
                    DeleteIfExists(row.FilePath);
                    DeleteIfExists(row.OriginalPath);
                    DeleteIfExists(_paths.ThumbnailPathFor(row.Id));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Could not delete the files of trashed capture {Id}; it stays in the Trash", row.Id);
                    continue;
                }

                // SQL, not tracked entities: the FK cascade would delete these rows first and EF would
                // then see "0 rows affected" for them.
                await db.Database.ExecuteSqlAsync($"DELETE FROM CaptureTag WHERE CaptureId = {row.Id}", cancellationToken);
                db.Captures.Remove(row);
                await db.Database.ExecuteSqlAsync($"DELETE FROM CaptureSearch WHERE CaptureId = {row.Id}", cancellationToken);
                removed++;
            }

            await db.SaveChangesAsync(cancellationToken);
            await RemoveUnusedTagsAsync(db, cancellationToken);
            _logger.LogInformation("Emptied the Trash: {Count} captures deleted", removed);
            return removed;
        }
        finally
        {
            _save.Release();
        }
    }

    /// <summary>Rewrites one capture's full-text row from the Capture and tag tables.</summary>
    private static async Task ReindexAsync(SnareDbContext db, Guid id, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlAsync($"DELETE FROM CaptureSearch WHERE CaptureId = {id}", cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO CaptureSearch (CaptureId, Title, Description, Tags, OcrText, SourceApp, SourceWindowTitle)
            SELECT c.Id, c.Title, c.Description,
                   (SELECT group_concat(t.Name, ' ') FROM CaptureTag ct JOIN Tag t ON t.Id = ct.TagId WHERE ct.CaptureId = c.Id),
                   c.OcrText, c.SourceApp, c.SourceWindowTitle
            FROM Capture c WHERE c.Id = {id}
            """,
            cancellationToken);
    }

    /// <summary>Finds or creates tags by name (trimmed, case-insensitive), giving new ones the next palette color.</summary>
    private static async Task<List<TagEntity>> ResolveTagsAsync(SnareDbContext db, IEnumerable<string> names, CancellationToken cancellationToken)
    {
        var wanted = names
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var existing = await db.Tags.ToListAsync(cancellationToken);
        var result = new List<TagEntity>();
        var created = 0;
        foreach (var name in wanted)
        {
            var tag = existing.Find(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (tag is null)
            {
                tag = new TagEntity { Name = name, Color = TagPalette.ForIndex(existing.Count + created++) };
                db.Tags.Add(tag);
            }

            result.Add(tag);
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static async Task RemoveUnusedTagsAsync(SnareDbContext db, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Tag WHERE Id NOT IN (SELECT TagId FROM CaptureTag)", cancellationToken);

    private static async Task<CaptureEntity> RequireAsync(SnareDbContext db, Guid id, CancellationToken cancellationToken) =>
        await db.Captures.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException($"No capture {id}.");

    private DateTime StartOfLocalDayUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, _clock.LocalTimeZone);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void DeleteIfExists(string? path)
    {
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
