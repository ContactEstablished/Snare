using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Snare.Library.Tests;

public class SqliteNativeTests
{
    [Fact]
    public void BundledSqlite_LoadsAndIsPatched()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "select sqlite_version()";
        var version = Version.Parse(Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture)!);

        // CVE-2025-6965 is fixed in SQLite 3.50.2.
        Assert.True(version >= new Version(3, 50, 2), $"SQLite {version} is older than 3.50.2");
    }

    [Fact]
    public void BundledSqlite_SupportsFts5()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            "create virtual table search using fts5(title, ocr_text);" +
            "insert into search values ('Invoice error', 'Payment failed');" +
            "select count(*) from search where search match 'invoic*';";

        Assert.Equal(1L, command.ExecuteScalar());
    }
}
