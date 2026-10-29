using Microsoft.EntityFrameworkCore;

namespace Snare.Library.Data;

/// <summary>
/// EF Core view of library.db. The schema itself comes from <see cref="SchemaMigrator"/>,
/// not EF migrations, so raw-SQL objects like the FTS5 index live alongside the tables.
/// </summary>
internal sealed class SnareDbContext(DbContextOptions<SnareDbContext> options) : DbContext(options)
{
    public DbSet<CaptureEntity> Captures => Set<CaptureEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var capture = modelBuilder.Entity<CaptureEntity>();
        capture.ToTable("Capture");
        capture.HasKey(c => c.Id);
        capture.Property(c => c.Kind).HasConversion<string>();
    }
}
