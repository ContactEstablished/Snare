using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Snare.Core.Captures;
using Snare.Core.Imaging;
using Snare.Core.Options;

namespace Snare.Library.Tests;

public class LibraryBackupTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (TempLibrary Library, LibraryBackup Backup, string Folder) Setup()
    {
        var library = new TempLibrary();
        var folder = library.Folder("Backups");
        library.Options.BackupFolder = folder;
        return (library, new LibraryBackup(library.Paths, library.Monitor, library.Clock, NullLogger<LibraryBackup>.Instance), folder);
    }

    [Fact]
    public async Task Backup_HoldsARestorableDatabaseAndEveryOriginal()
    {
        var (library, backup, folder) = Setup();
        using var _ = library;
        await library.Store.SaveImageAsync(PixelBuffer.Filled(8, 8, 0xFF112233), CaptureSource.Unknown, library.Clock.Now, cancellationToken: Ct);
        await library.Store.SaveImageAsync(PixelBuffer.Filled(8, 8, 0xFF445566), CaptureSource.Unknown, library.Clock.Now.AddSeconds(1), cancellationToken: Ct);

        var zip = await backup.CreateAsync(Ct);

        Assert.Equal(Path.Combine(folder, "Snare-backup-2026-10-28_093015.zip"), zip.FullName);
        using var archive = ZipFile.OpenRead(zip.FullName);
        Assert.Equal(2, archive.Entries.Count(e => e.FullName.StartsWith(LibraryBackup.OriginalsEntry + "/", StringComparison.Ordinal)));

        var restored = library.Folder("restored.db");
        archive.GetEntry(LibraryBackup.DatabaseEntry)!.ExtractToFile(restored);
        using var connection = new SqliteConnection($"Data Source={restored};Pooling=False");
        connection.Open();
        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Capture";
        Assert.Equal(2L, count.ExecuteScalar());
        Assert.NotNull(backup.LastBackupTime());
    }

    [Fact]
    public async Task Backup_KeepsTheTenNewest_AndLeavesOtherFilesAlone()
    {
        var (library, backup, folder) = Setup();
        using var _ = library;
        await library.Store.SaveImageAsync(PixelBuffer.Filled(4, 4, 0xFF000000), CaptureSource.Unknown, library.Clock.Now, cancellationToken: Ct);
        Directory.CreateDirectory(folder);
        for (var day = 1; day <= 12; day++)
        {
            File.WriteAllText(Path.Combine(folder, $"Snare-backup-2026-09-{day:00}_120000.zip"), "old");
        }

        File.WriteAllText(Path.Combine(folder, "notes.txt"), "mine");

        await backup.CreateAsync(Ct);
        await backup.CreateAsync(Ct); // same second: gets _2

        var zips = Directory.GetFiles(folder, "*.zip").Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(10, zips.Count);
        Assert.Contains("Snare-backup-2026-10-28_093015.zip", zips);
        Assert.Contains("Snare-backup-2026-10-28_093015_2.zip", zips);
        Assert.DoesNotContain("Snare-backup-2026-09-04_120000.zip", zips); // the four oldest went
        Assert.Contains("Snare-backup-2026-09-05_120000.zip", zips);
        Assert.True(File.Exists(Path.Combine(folder, "notes.txt")));
    }

    [Fact]
    public async Task AFailedBackup_LeavesNothingBehind()
    {
        var (library, backup, folder) = Setup();
        using var _ = library;
        var record = await library.Store.SaveImageAsync(PixelBuffer.Filled(4, 4, 0xFF000000), CaptureSource.Unknown, library.Clock.Now, cancellationToken: Ct);

        using (new FileStream(record.OriginalPath!, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => backup.CreateAsync(Ct));
        }

        Assert.Empty(Directory.GetFiles(folder));
        Assert.Null(backup.LastBackupTime());
    }

    [Fact]
    public async Task AnEmptyLibrary_SaysThereIsNothingToBackUp()
    {
        var (library, backup, _) = Setup();
        using var __ = library;

        await Assert.ThrowsAsync<InvalidOperationException>(() => backup.CreateAsync(Ct));
    }

    [Fact]
    public void LibraryRoot_FollowsTheSetting()
    {
        var options = new SnareOptions { LibraryRoot = @"D:\First" };
        var monitor = new Monitor(options);
        var paths = LibraryPaths.FromOptions(monitor);

        Assert.Equal(@"D:\First", paths.LibraryRoot);
        options.LibraryRoot = @"E:\Second";
        Assert.StartsWith(@"E:\Second\2026\10\", paths.ExportPathFor(new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero), ExportFormat.Png), StringComparison.Ordinal);
        options.LibraryRoot = "";
        Assert.Equal(LibraryPaths.DefaultLibraryRoot, paths.LibraryRoot);
    }

    private sealed class Monitor(SnareOptions value) : Microsoft.Extensions.Options.IOptionsMonitor<SnareOptions>
    {
        public SnareOptions CurrentValue => value;

        public SnareOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<SnareOptions, string?> listener) => null;
    }
}
