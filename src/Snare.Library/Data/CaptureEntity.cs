using Snare.Core.Captures;

namespace Snare.Library.Data;

/// <summary>EF Core row for the Capture table. Times are UTC <see cref="DateTime"/>s so SQLite can sort them.</summary>
internal sealed class CaptureEntity
{
    public Guid Id { get; set; }

    public CaptureKind Kind { get; set; }

    public string FilePath { get; set; } = "";

    public string? OriginalPath { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public long? DurationMs { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    public string? SourceApp { get; set; }

    public string? SourceWindowTitle { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? OcrText { get; set; }

    public string? AnnotationJson { get; set; }

    public bool IsDeleted { get; set; }

    public CaptureRecord ToRecord() => new()
    {
        Id = Id,
        Kind = Kind,
        FilePath = FilePath,
        OriginalPath = OriginalPath,
        Width = Width,
        Height = Height,
        DurationMs = DurationMs,
        CreatedUtc = new DateTimeOffset(DateTime.SpecifyKind(CreatedUtc, DateTimeKind.Utc)),
        ModifiedUtc = new DateTimeOffset(DateTime.SpecifyKind(ModifiedUtc, DateTimeKind.Utc)),
        SourceApp = SourceApp,
        SourceWindowTitle = SourceWindowTitle,
        Title = Title,
        Description = Description,
        OcrText = OcrText,
        AnnotationJson = AnnotationJson,
        IsDeleted = IsDeleted,
    };
}
