using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Snare.Library.Data;

/// <summary>
/// Brings library.db up to the current schema by running each script whose number is above
/// <c>PRAGMA user_version</c>, in one transaction per script. Append new scripts; never edit old ones.
/// </summary>
internal static class SchemaMigrator
{
    internal static readonly string[] Scripts =
    [
        // 1: Phase 1 tables. Tag and CaptureTag are mapped by EF in Phase 3.
        """
        CREATE TABLE Capture (
            Id TEXT NOT NULL PRIMARY KEY,
            Kind TEXT NOT NULL,
            FilePath TEXT NOT NULL,
            OriginalPath TEXT NULL,
            Width INTEGER NOT NULL,
            Height INTEGER NOT NULL,
            DurationMs INTEGER NULL,
            CreatedUtc TEXT NOT NULL,
            ModifiedUtc TEXT NOT NULL,
            SourceApp TEXT NULL,
            SourceWindowTitle TEXT NULL,
            Title TEXT NULL,
            Description TEXT NULL,
            OcrText TEXT NULL,
            AnnotationJson TEXT NULL,
            IsDeleted INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IX_Capture_CreatedUtc ON Capture (CreatedUtc);
        CREATE TABLE Tag (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL COLLATE NOCASE UNIQUE,
            Color TEXT NULL
        );
        CREATE TABLE CaptureTag (
            CaptureId TEXT NOT NULL REFERENCES Capture (Id) ON DELETE CASCADE,
            TagId INTEGER NOT NULL REFERENCES Tag (Id) ON DELETE CASCADE,
            PRIMARY KEY (CaptureId, TagId)
        );
        """,

        // 2: Phase 3 full-text index, backfilled from existing captures.
        """
        CREATE VIRTUAL TABLE CaptureSearch USING fts5(
            CaptureId UNINDEXED, Title, Description, Tags, OcrText, SourceApp, SourceWindowTitle,
            tokenize = 'unicode61 remove_diacritics 2'
        );
        CREATE INDEX IX_CaptureTag_TagId ON CaptureTag (TagId);
        INSERT INTO CaptureSearch (CaptureId, Title, Description, Tags, OcrText, SourceApp, SourceWindowTitle)
        SELECT c.Id, c.Title, c.Description,
               (SELECT group_concat(t.Name, ' ') FROM CaptureTag ct JOIN Tag t ON t.Id = ct.TagId WHERE ct.CaptureId = c.Id),
               c.OcrText, c.SourceApp, c.SourceWindowTitle
        FROM Capture c;
        """,
    ];

    public static int CurrentVersion => Scripts.Length;

    public static async Task MigrateAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var version = await GetVersionAsync(connection, cancellationToken);
        for (var next = version + 1; next <= Scripts.Length; next++)
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = Scripts[next - 1] + string.Create(CultureInfo.InvariantCulture, $";\nPRAGMA user_version = {next};");
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }

    public static async Task<int> GetVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }
}
