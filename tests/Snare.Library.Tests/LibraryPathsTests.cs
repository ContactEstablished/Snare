using Snare.Core.Options;

namespace Snare.Library.Tests;

public class LibraryPathsTests
{
    private static readonly LibraryPaths Paths = new(
        Path.Combine("C:", "Users", "matt", "Pictures", "Snare"),
        Path.Combine("C:", "Users", "matt", "AppData", "Local", "Snare"));

    private static readonly DateTimeOffset CapturedAt = new(2026, 10, 25, 18, 23, 1, TimeSpan.FromHours(-4));

    [Fact]
    public void ExportPath_IsFiledByYearAndMonth()
    {
        var expected = Path.Combine(Paths.LibraryRoot, "2026", "10", "2026-10-25_182301.png");

        Assert.Equal(expected, Paths.ExportPathFor(CapturedAt, ExportFormat.Png));
    }

    [Fact]
    public void ExportPath_UsesJpgExtensionForJpeg()
    {
        Assert.EndsWith(".jpg", Paths.ExportPathFor(CapturedAt, ExportFormat.Jpeg), StringComparison.Ordinal);
    }

    [Fact]
    public void UniqueExportPath_AddsTheNextFreeSuffix()
    {
        var taken = new HashSet<string>
        {
            Paths.ExportPathFor(CapturedAt, ExportFormat.Png),
            Path.Combine(Paths.LibraryRoot, "2026", "10", "2026-10-25_182301_2.png"),
        };

        var path = Paths.UniqueExportPathFor(CapturedAt, ExportFormat.Png, taken.Contains);

        Assert.Equal(Path.Combine(Paths.LibraryRoot, "2026", "10", "2026-10-25_182301_3.png"), path);
    }

    [Fact]
    public void UniqueExportPath_IsThePlainNameWhenFree()
    {
        Assert.Equal(
            Paths.ExportPathFor(CapturedAt, ExportFormat.Png),
            Paths.UniqueExportPathFor(CapturedAt, ExportFormat.Png, _ => false));
    }

    [Fact]
    public void PrivateFiles_StayUnderAppData()
    {
        Assert.StartsWith(Paths.AppDataRoot, Paths.DatabasePath, StringComparison.Ordinal);
        Assert.StartsWith(Paths.AppDataRoot, Paths.OriginalsFolder, StringComparison.Ordinal);
        Assert.StartsWith(Paths.AppDataRoot, Paths.ThumbnailsFolder, StringComparison.Ordinal);
    }

    [Fact]
    public void FromOptions_UsesTheConfiguredLibraryRoot()
    {
        var root = Path.Combine("D:", "Captures");

        var paths = LibraryPaths.FromOptions(new SnareOptions { LibraryRoot = root });

        Assert.Equal(root, paths.LibraryRoot);
    }
}
