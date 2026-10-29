using System.Globalization;
using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Snare.Core.Options;

namespace Snare.Library;

/// <summary>
/// One-click backup (decided): a zip of library.db and the originals folder, written to the
/// backup folder as Snare-backup-yyyy-MM-dd_HHmmss.zip; the 10 newest are kept. Exports and
/// videos aren't included (they're already plain files in Pictures). Restore by hand: quit
/// Snare and unzip over %LOCALAPPDATA%\Snare.
/// </summary>
public sealed class LibraryBackup(
    LibraryPaths paths, IOptionsMonitor<SnareOptions> options, TimeProvider clock, ILogger<LibraryBackup> logger)
{
    public const int Keep = 10;
    public const string DatabaseEntry = "library.db";
    public const string OriginalsEntry = "originals";
    private const string Prefix = "Snare-backup-";

    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Snare Backups");

    public string Folder => string.IsNullOrWhiteSpace(options.CurrentValue.BackupFolder) ? DefaultFolder : options.CurrentValue.BackupFolder;

    /// <summary>When the newest backup in the folder was written, or null if there is none.</summary>
    public DateTimeOffset? LastBackupTime()
    {
        var newest = Backups(Folder).FirstOrDefault();
        return newest is null ? null : new DateTimeOffset(newest.LastWriteTimeUtc, TimeSpan.Zero);
    }

    /// <summary>
    /// Writes a new backup and prunes old ones. The zip appears under its final name only once
    /// it's complete; on any failure nothing is left behind.
    /// </summary>
    public Task<FileInfo> CreateAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Create(cancellationToken), cancellationToken);

    private FileInfo Create(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.DatabasePath))
        {
            throw new InvalidOperationException("The library is empty, so there is nothing to back up yet.");
        }

        var folder = Folder;
        Directory.CreateDirectory(folder);
        var path = UniquePath(folder, clock.GetLocalNow());
        var partial = path + ".partial";
        var databaseCopy = Path.Combine(Path.GetTempPath(), $"snare-backup-{Guid.NewGuid():N}.db");
        try
        {
            CopyDatabase(databaseCopy);
            using (var zip = ZipFile.Open(partial, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(databaseCopy, DatabaseEntry, CompressionLevel.Optimal);
                if (Directory.Exists(paths.OriginalsFolder))
                {
                    foreach (var original in Directory.EnumerateFiles(paths.OriginalsFolder))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        // PNGs are already compressed; storing them is as small and much faster.
                        zip.CreateEntryFromFile(original, $"{OriginalsEntry}/{Path.GetFileName(original)}", CompressionLevel.NoCompression);
                    }
                }
            }

            File.Move(partial, path);
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
        finally
        {
            TryDelete(databaseCopy);
        }

        Prune(folder);
        var result = new FileInfo(path);
        logger.LogInformation("Backed up the library to {Path} ({Bytes:N0} bytes)", path, result.Length);
        return result;
    }

    /// <summary>A consistent copy through SQLite's online backup, safe while Snare is writing.</summary>
    private void CopyDatabase(string destination)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        using var source = new SqliteConnection(builder.ToString());
        using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination, Pooling = false }.ToString());
        source.Open();
        target.Open();
        source.BackupDatabase(target);
    }

    /// <summary>Keeps the <see cref="Keep"/> newest backups; never touches other files.</summary>
    private void Prune(string folder)
    {
        foreach (var old in Backups(folder).Skip(Keep))
        {
            try
            {
                old.Delete();
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Could not delete old backup {Path}", old.FullName);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Could not delete old backup {Path}", old.FullName);
            }
        }
    }

    /// <summary>Backups newest first. Names sort by time, so ordinal descending is newest first.</summary>
    private static IEnumerable<FileInfo> Backups(string folder) =>
        Directory.Exists(folder)
            ? new DirectoryInfo(folder).EnumerateFiles($"{Prefix}*.zip").OrderByDescending(f => f.Name, StringComparer.Ordinal)
            : [];

    private static string UniquePath(string folder, DateTimeOffset now)
    {
        var stem = Prefix + now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(folder, stem + ".zip");
        for (var n = 2; File.Exists(path); n++)
        {
            path = Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"{stem}_{n}.zip"));
        }

        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
