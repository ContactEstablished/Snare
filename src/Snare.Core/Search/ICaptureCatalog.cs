using Snare.Core.Captures;

namespace Snare.Core.Search;

/// <summary>What the library knows about captures: details, tags, OCR text, search and the Trash.</summary>
public interface ICaptureCatalog
{
    /// <summary>Raised after an image capture is saved or re-exported, so its text can be read again.</summary>
    event EventHandler<Guid>? ImageChanged;

    Task<IReadOnlyList<CaptureRecord>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default);

    Task<CaptureRecord> UpdateDetailsAsync(Guid id, string? title, string? description, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetTagsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Replaces a capture's tags. New names become tags with the next palette color; unused tags are removed.</summary>
    Task SetTagsAsync(Guid id, IEnumerable<string> names, CancellationToken cancellationToken = default);

    /// <summary>Adds tags to several captures at once, keeping their existing tags.</summary>
    Task AddTagsAsync(IEnumerable<Guid> ids, IEnumerable<string> names, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TagInfo>> ListTagsAsync(CancellationToken cancellationToken = default);

    Task SetTagColorAsync(string name, string color, CancellationToken cancellationToken = default);

    Task SetOcrTextAsync(Guid id, string text, CancellationToken cancellationToken = default);

    /// <summary>Image captures whose text hasn't been read yet.</summary>
    Task<IReadOnlyList<Guid>> ListAwaitingOcrAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CaptureRecord>> ListTrashAsync(CancellationToken cancellationToken = default);

    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes every trashed capture's files and rows. Returns how many were removed.</summary>
    Task<int> EmptyTrashAsync(CancellationToken cancellationToken = default);
}
