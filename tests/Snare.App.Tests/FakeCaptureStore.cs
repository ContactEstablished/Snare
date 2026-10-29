using System.IO;
using Snare.Core.Annotations;
using Snare.Core.Captures;
using Snare.Core.Imaging;
using Snare.Core.Search;

namespace Snare.App.Tests;

internal sealed class FakeCaptureStore : ICaptureStore, ICaptureCatalog
{
    public List<CaptureRecord> Captures { get; } = [];

    public List<Guid> Trashed { get; } = [];

    public static CaptureRecord Record(int minute) => new()
    {
        Id = Guid.CreateVersion7(),
        Kind = CaptureKind.Image,
        FilePath = $@"C:\nowhere\2026-10-28_09{minute:00}00.png",
        Width = 800,
        Height = 600,
        CreatedUtc = new DateTimeOffset(2026, 10, 28, 13, minute, 0, TimeSpan.Zero),
        ModifiedUtc = new DateTimeOffset(2026, 10, 28, 13, minute, 0, TimeSpan.Zero),
        SourceApp = "chrome",
    };

    public Task<CaptureRecord> SaveImageAsync(PixelBuffer image, CaptureSource source, DateTimeOffset capturedAt, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<CaptureRecord>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CaptureRecord>>(Captures.Where(c => !Trashed.Contains(c.Id)).OrderByDescending(c => c.CreatedUtc).ToList());

    public Task<CaptureRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Captures.FirstOrDefault(c => c.Id == id));

    public Task MoveToTrashAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Trashed.Add(id);
        return Task.CompletedTask;
    }

    public string ThumbnailPathFor(Guid id) => $@"C:\nowhere\{id:N}.webp";

    public Task<PixelBuffer> LoadOriginalAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(PixelBuffer.Filled(800, 600, 0xFF336699));

    public Task SaveAnnotationsAsync(Guid id, AnnotationDocument document, CancellationToken cancellationToken = default)
    {
        AnnotationSaves++;
        return Task.CompletedTask;
    }

    public int ReexportCount { get; private set; }

    public bool FailReexport { get; set; }

    public int AnnotationSaves { get; private set; }

    public Task<CaptureRecord> ReexportAsync(Guid id, AnnotationDocument document, CancellationToken cancellationToken = default)
    {
        ReexportCount++;
        if (FailReexport)
        {
            throw new IOException("The export is locked.");
        }

        return Task.FromResult(Captures.First(c => c.Id == id));
    }

    public Task<CaptureRecord> DiscardOriginalAsync(Guid id, AnnotationDocument document, CancellationToken cancellationToken = default) =>
        Task.FromResult(Captures.First(c => c.Id == id) with { OriginalPath = null });

    public string NewVideoPath(DateTimeOffset capturedAt) => Path.Combine(Path.GetTempPath(), $"snare-test-{Guid.NewGuid():N}.mp4");

    public Task<CaptureRecord> SaveVideoAsync(
        string path, int width, int height, long durationMs, CaptureSource source, DateTimeOffset capturedAt, byte[]? thumbnailPng,
        CancellationToken cancellationToken = default)
    {
        var record = Record(0) with { Kind = CaptureKind.Video, FilePath = path, Width = width, Height = height, DurationMs = durationMs };
        Captures.Add(record);
        return Task.FromResult(record);
    }

    public Task<CaptureRecord> UpdateVideoAsync(Guid id, long durationMs, byte[]? thumbnailPng, CancellationToken cancellationToken = default)
    {
        var index = Captures.FindIndex(c => c.Id == id);
        Captures[index] = Captures[index] with { DurationMs = durationMs };
        return Task.FromResult(Captures[index]);
    }

    // ---- ICaptureCatalog, in memory ----

    public Dictionary<Guid, List<string>> TagsById { get; } = [];

    public event EventHandler<Guid>? ImageChanged;

    public Task<IReadOnlyList<CaptureRecord>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CaptureRecord>>(Captures
            .Where(c => !Trashed.Contains(c.Id))
            .Where(c => query.Terms.All(t => (c.Title ?? "").Contains(t, StringComparison.OrdinalIgnoreCase)))
            .Where(c => query.Tags.All(t => TagsById.GetValueOrDefault(c.Id)?.Contains(t, StringComparer.OrdinalIgnoreCase) == true))
            .OrderByDescending(c => c.CreatedUtc)
            .ToList());

    public Task<CaptureRecord> UpdateDetailsAsync(Guid id, string? title, string? description, CancellationToken cancellationToken = default)
    {
        var index = Captures.FindIndex(c => c.Id == id);
        Captures[index] = Captures[index] with { Title = title, Description = description };
        return Task.FromResult(Captures[index]);
    }

    public Task<IReadOnlyList<string>> GetTagsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(TagsById.GetValueOrDefault(id) ?? []);

    public Task SetTagsAsync(Guid id, IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        TagsById[id] = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return Task.CompletedTask;
    }

    public Task AddTagsAsync(IEnumerable<Guid> ids, IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        foreach (var id in ids)
        {
            TagsById[id] = (TagsById.GetValueOrDefault(id) ?? []).Concat(names).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TagInfo>> ListTagsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TagInfo>>(TagsById.Values.SelectMany(t => t).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((name, i) => new TagInfo(name, TagPalette.ForIndex(i), 1)).ToList());

    public Task SetTagColorAsync(string name, string color, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SetOcrTextAsync(Guid id, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<Guid>> ListAwaitingOcrAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>([]);

    public Task<IReadOnlyList<CaptureRecord>> ListTrashAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CaptureRecord>>(Captures.Where(c => Trashed.Contains(c.Id)).ToList());

    public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Trashed.Remove(id);
        return Task.CompletedTask;
    }

    public Task<int> EmptyTrashAsync(CancellationToken cancellationToken = default)
    {
        var count = Captures.RemoveAll(c => Trashed.Contains(c.Id));
        Trashed.Clear();
        return Task.FromResult(count);
    }

    public void RaiseImageChanged(Guid id) => ImageChanged?.Invoke(this, id);
}
