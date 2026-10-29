using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Snare.Core.Options;
using Snare.Rendering;

namespace Snare.Library.Tests;

/// <summary>A throwaway library under %TEMP%, deleted when the test ends.</summary>
internal sealed class TempLibrary : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("snare-library-");

    public TempLibrary(SnareOptions? options = null)
    {
        Paths = new LibraryPaths(Path.Combine(_root.FullName, "Pictures"), Path.Combine(_root.FullName, "AppData"));
        Options = options ?? new SnareOptions();
        Monitor = new FixedOptions(Options);
        Store = new SqliteCaptureStore(
            Paths, Monitor, new AnnotationRenderer(), Clock, NullLogger<SqliteCaptureStore>.Instance);
    }

    public LibraryPaths Paths { get; }

    public SnareOptions Options { get; }

    public IOptionsMonitor<SnareOptions> Monitor { get; }

    /// <summary>A scratch folder inside this library's temp root, removed with it.</summary>
    public string Folder(string name) => Path.Combine(_root.FullName, name);

    public TestClock Clock { get; } = new(new DateTimeOffset(2026, 10, 28, 9, 30, 15, TimeSpan.FromHours(-4)));

    public SqliteCaptureStore Store { get; }

    public SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection($"Data Source={Paths.DatabasePath}");
        connection.Open();
        return connection;
    }

    public void Dispose()
    {
        // Pooled connections keep library.db open. Only this library's pools: ClearAllPools would
        // also dispose connections that tests running in parallel are using.
        foreach (var connectionString in new[] { $"Data Source={Paths.DatabasePath}", new SqliteConnectionStringBuilder { DataSource = Paths.DatabasePath }.ToString() })
        {
            using var connection = new SqliteConnection(connectionString);
            SqliteConnection.ClearPool(connection);
        }

        _root.Delete(recursive: true);
    }

    private sealed class FixedOptions(SnareOptions value) : IOptionsMonitor<SnareOptions>
    {
        public SnareOptions CurrentValue => value;

        public SnareOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<SnareOptions, string?> listener) => null;
    }
}
