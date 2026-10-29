using Snare.Core.Annotations;
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
        PixelBuffer image, CaptureSource source, DateTimeOffset capturedAt, CaptureKind kind = CaptureKind.Image,
        CancellationToken cancellationToken = default);

    /// <summary>Every capture not in the Trash, newest first.</summary>
    Task<IReadOnlyList<CaptureRecord>> ListAsync(CancellationToken cancellationToken = default);

    Task<CaptureRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The path a new recording should be written to (it lives in the library from the start).</summary>
    string NewVideoPath(DateTimeOffset capturedAt);

    /// <summary>Adds a finished recording, already at <paramref name="path"/>, with an optional PNG thumbnail.</summary>
    Task<CaptureRecord> SaveVideoAsync(
        string path, int width, int height, long durationMs, CaptureSource source, DateTimeOffset capturedAt, byte[]? thumbnailPng,
        CancellationToken cancellationToken = default);

    /// <summary>After the file was replaced in place (a trim): new duration and thumbnail.</summary>
    Task<CaptureRecord> UpdateVideoAsync(Guid id, long durationMs, byte[]? thumbnailPng, CancellationToken cancellationToken = default);

    /// <summary>Soft delete; files stay until the Trash is emptied.</summary>
    Task MoveToTrashAsync(Guid id, CancellationToken cancellationToken = default);

    string ThumbnailPathFor(Guid id);

    /// <summary>The untouched pixels for editing, or the export's pixels once the original is discarded.</summary>
    Task<PixelBuffer> LoadOriginalAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Autosave: stores the editable annotation layer without re-exporting.</summary>
    Task SaveAnnotationsAsync(Guid id, AnnotationDocument document, CancellationToken cancellationToken = default);

    /// <summary>Renders the original plus annotations and overwrites the export and thumbnail in place.</summary>
    Task<CaptureRecord> ReexportAsync(Guid id, AnnotationDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-exports, then deletes the original so redactions are permanent. The capture can't be
    /// re-edited afterwards; the export is its only copy.
    /// </summary>
    Task<CaptureRecord> DiscardOriginalAsync(Guid id, AnnotationDocument document, CancellationToken cancellationToken = default);
}
