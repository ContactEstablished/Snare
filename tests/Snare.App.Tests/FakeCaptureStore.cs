using Snare.Core.Annotations;
using Snare.Core.Captures;
using Snare.Core.Imaging;

namespace Snare.App.Tests;

internal sealed class FakeCaptureStore : ICaptureStore
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

    public Task SaveAnnotationsAsync(Guid id, AnnotationDocument document, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<CaptureRecord> ReexportAsync(Guid id, AnnotationDocument document, CancellationToken cancellationToken = default) =>
        Task.FromResult(Captures.First(c => c.Id == id));

    public Task<CaptureRecord> DiscardOriginalAsync(Guid id, AnnotationDocument document, CancellationToken cancellationToken = default) =>
        Task.FromResult(Captures.First(c => c.Id == id) with { OriginalPath = null });
}
