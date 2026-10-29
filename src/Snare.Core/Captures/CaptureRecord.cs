namespace Snare.Core.Captures;

/// <summary>One capture in the library: where its files are and what is known about it.</summary>
public sealed record CaptureRecord
{
    /// <summary>A version 7 GUID, so ids sort by creation time.</summary>
    public required Guid Id { get; init; }

    public required CaptureKind Kind { get; init; }

    /// <summary>The flattened export (PNG/JPG) or recording (MP4) under the library root.</summary>
    public required string FilePath { get; init; }

    /// <summary>The untouched pixels kept for re-editing; null once discarded.</summary>
    public string? OriginalPath { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public long? DurationMs { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public required DateTimeOffset ModifiedUtc { get; init; }

    public string? SourceApp { get; init; }

    public string? SourceWindowTitle { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? OcrText { get; init; }

    public string? AnnotationJson { get; init; }

    public bool IsDeleted { get; init; }
}
