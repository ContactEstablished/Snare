namespace Snare.Library.Data;

/// <summary>EF Core row for the CaptureTag join table.</summary>
internal sealed class CaptureTagEntity
{
    public Guid CaptureId { get; set; }

    public int TagId { get; set; }
}
