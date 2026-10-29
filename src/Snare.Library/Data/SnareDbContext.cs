using Microsoft.EntityFrameworkCore;

namespace Snare.Library.Data;

/// <summary>
/// EF Core view of library.db. The schema itself comes from <see cref="SchemaMigrator"/>,
/// not EF migrations, so raw-SQL objects like the FTS5 index live alongside the tables.
/// </summary>
internal sealed class SnareDbContext(DbContextOptions<SnareDbContext> options) : DbContext(options)
{
    public DbSet<CaptureEntity> Captures => Set<CaptureEntity>();

    public DbSet<TagEntity> Tags => Set<TagEntity>();

    public DbSet<CaptureTagEntity> CaptureTags => Set<CaptureTagEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var capture = modelBuilder.Entity<CaptureEntity>();
        capture.ToTable("Capture");
        capture.HasKey(c => c.Id);
        capture.Property(c => c.Kind).HasConversion<string>();

        var tag = modelBuilder.Entity<TagEntity>();
        tag.ToTable("Tag");
        tag.HasKey(t => t.Id);
        tag.Property(t => t.Name).UseCollation("NOCASE");

        var captureTag = modelBuilder.Entity<CaptureTagEntity>();
        captureTag.ToTable("CaptureTag");
        captureTag.HasKey(ct => new { ct.CaptureId, ct.TagId });
    }
}
