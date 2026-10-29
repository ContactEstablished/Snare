using Snare.Core.Imaging;

namespace Snare.Core.Captures;

/// <summary>The library: saves captures and finds them again.</summary>
public interface ICaptureStore
{
    /// <summary>
    /// Saves a new still capture: the original first, then the database row, then the thumbnail
    /// and the flattened export, so a crash never loses pixels. If only the export fails, the
    /// capture stays in the library and the error is rethrown.
    /// </summary>
    Task<CaptureRecord> SaveImageAsync(
        PixelBuffer image, CaptureSource source, DateTimeOffset capturedAt, CancellationToken cancellationToken = default);

    /// <summary>Every capture not in the Trash, newest first.</summary>
    Task<IReadOnlyList<CaptureRecord>> ListAsync(CancellationToken cancellationToken = default);

    Task<CaptureRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Soft delete; files stay until the Trash is emptied.</summary>
    Task MoveToTrashAsync(Guid id, CancellationToken cancellationToken = default);

    string ThumbnailPathFor(Guid id);
}
