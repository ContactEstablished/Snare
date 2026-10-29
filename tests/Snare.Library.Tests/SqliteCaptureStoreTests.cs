using Snare.Core.Captures;
using Snare.Core.Imaging;
using Snare.Core.Options;
using Snare.Library.Data;
using Snare.Rendering;

namespace Snare.Library.Tests;

public class SqliteCaptureStoreTests
{
    private static readonly DateTimeOffset CapturedAt = new(2026, 10, 28, 9, 30, 15, TimeSpan.FromHours(-4));
    private static readonly CaptureSource Chrome = new("chrome", "Invoice #1234 - Google Chrome");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Save_WritesOriginalRowExportAndThumbnail()
    {
        using var library = new TempLibrary();

        var record = await library.Store.SaveImageAsync(PixelBuffer.Filled(40, 20, 0xFF1F6FEB), Chrome, CapturedAt, Ct);

        Assert.Equal(Path.Combine(library.Paths.LibraryRoot, "2026", "10", "2026-10-28_093015.png"), record.FilePath);
        Assert.True(File.Exists(record.FilePath));
        Assert.True(File.Exists(record.OriginalPath));
        Assert.True(File.Exists(library.Store.ThumbnailPathFor(record.Id)));
        Assert.Equal((40, 20), (record.Width, record.Height));
        Assert.Equal(CaptureKind.Image, record.Kind);
        Assert.Equal("chrome", record.SourceApp);
        Assert.Equal(CapturedAt, record.CreatedUtc);
        Assert.Equal(7, record.Id.Version);

        var stored = await library.Store.GetAsync(record.Id, Ct);
        Assert.Equal(record, stored);
    }

    [Fact]
    public async Task Save_ExportsTheSamePixels()
    {
        using var library = new TempLibrary();
        var image = PixelBuffer.Filled(4, 4, 0xFF112233);
        image.Pixels[(2 * image.Stride) + 4] = 0xFF; // (1, 2) blue

        var record = await library.Store.SaveImageAsync(image, Chrome, CapturedAt, Ct);

        using var export = ImageCodec.Decode(await File.ReadAllBytesAsync(record.FilePath, Ct));
        using var original = ImageCodec.Decode(await File.ReadAllBytesAsync(record.OriginalPath!, Ct));
        Assert.Equal(new SkiaSharp.SKColor(0x11, 0x22, 0xFF), export.GetPixel(1, 2));
        Assert.Equal(new SkiaSharp.SKColor(0x11, 0x22, 0x33), original.GetPixel(0, 0));
    }

    [Fact]
    public async Task Save_InTheSameSecond_AddsASuffix()
    {
        using var library = new TempLibrary();

        var first = await library.Store.SaveImageAsync(PixelBuffer.Filled(2, 2, 0xFF000000), Chrome, CapturedAt, Ct);
        var second = await library.Store.SaveImageAsync(PixelBuffer.Filled(2, 2, 0xFF000000), Chrome, CapturedAt, Ct);
        var third = await library.Store.SaveImageAsync(PixelBuffer.Filled(2, 2, 0xFF000000), Chrome, CapturedAt, Ct);

        Assert.EndsWith("2026-10-28_093015.png", first.FilePath, StringComparison.Ordinal);
        Assert.EndsWith("2026-10-28_093015_2.png", second.FilePath, StringComparison.Ordinal);
        Assert.EndsWith("2026-10-28_093015_3.png", third.FilePath, StringComparison.Ordinal);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Save_AsJpeg_UsesTheJpgExtension()
    {
        using var library = new TempLibrary(new SnareOptions { DefaultExportFormat = ExportFormat.Jpeg, JpegQuality = 80 });

        var record = await library.Store.SaveImageAsync(PixelBuffer.Filled(8, 8, 0xFFFFFFFF), Chrome, CapturedAt, Ct);

        Assert.EndsWith(".jpg", record.FilePath, StringComparison.Ordinal);
        var bytes = await File.ReadAllBytesAsync(record.FilePath, Ct);
        Assert.Equal(new byte[] { 0xFF, 0xD8 }, bytes[..2]);
    }

    [Fact]
    public async Task List_IsNewestFirstAndSkipsTheTrash()
    {
        using var library = new TempLibrary();
        var older = await library.Store.SaveImageAsync(PixelBuffer.Filled(2, 2, 0), Chrome, CapturedAt, Ct);
        var newer = await library.Store.SaveImageAsync(PixelBuffer.Filled(2, 2, 0), Chrome, CapturedAt.AddMinutes(1), Ct);
        var trashed = await library.Store.SaveImageAsync(PixelBuffer.Filled(2, 2, 0), Chrome, CapturedAt.AddMinutes(2), Ct);

        await library.Store.MoveToTrashAsync(trashed.Id, Ct);
        var list = await library.Store.ListAsync(Ct);

        Assert.Equal([newer.Id, older.Id], list.Select(c => c.Id));
        Assert.True((await library.Store.GetAsync(trashed.Id, Ct))!.IsDeleted);
        Assert.True(File.Exists(trashed.FilePath)); // files stay until the Trash is emptied
    }

    [Fact]
    public async Task Save_WhenTheExportFails_KeepsOriginalRowAndThumbnail()
    {
        using var library = new TempLibrary();
        // A file where the year folder should be makes the export's directory creation fail.
        Directory.CreateDirectory(library.Paths.LibraryRoot);
        await File.WriteAllTextAsync(Path.Combine(library.Paths.LibraryRoot, "2026"), "in the way", Ct);

        await Assert.ThrowsAnyAsync<IOException>(() =>
            library.Store.SaveImageAsync(PixelBuffer.Filled(4, 4, 0xFF000000), Chrome, CapturedAt, Ct));

        var saved = Assert.Single(await library.Store.ListAsync(Ct));
        Assert.True(File.Exists(saved.OriginalPath));
        Assert.True(File.Exists(library.Store.ThumbnailPathFor(saved.Id)));
        Assert.False(File.Exists(saved.FilePath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(library.Paths.LibraryRoot)!, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Save_Concurrently_InTheSameSecond_GetsDistinctExports()
    {
        using var library = new TempLibrary();

        var records = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            library.Store.SaveImageAsync(PixelBuffer.Filled(2, 2, 0xFF000000), Chrome, CapturedAt, Ct)));

        Assert.Equal(4, records.Select(r => r.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(records, r => Assert.True(File.Exists(r.FilePath)));
    }

    [Fact]
    public async Task Thumbnail_IsAWebpInside320x240()
    {
        using var library = new TempLibrary();

        var record = await library.Store.SaveImageAsync(PixelBuffer.Filled(1280, 360, 0xFF1F6FEB), Chrome, CapturedAt, Ct);

        using var thumb = ImageCodec.Decode(await File.ReadAllBytesAsync(library.Store.ThumbnailPathFor(record.Id), Ct));
        Assert.Equal((320, 90), (thumb.Width, thumb.Height));
    }

    [Fact]
    public async Task Database_IsAtTheCurrentSchemaVersion()
    {
        using var library = new TempLibrary();
        await library.Store.ListAsync(Ct);

        using var connection = library.OpenDatabase();

        Assert.Equal(SchemaMigrator.CurrentVersion, await SchemaMigrator.GetVersionAsync(connection, Ct));
    }

    [Fact]
    public async Task Migrate_IsSafeToRunAgain()
    {
        using var library = new TempLibrary();
        await library.Store.SaveImageAsync(PixelBuffer.Filled(2, 2, 0), Chrome, CapturedAt, Ct);

        using var connection = library.OpenDatabase();
        await SchemaMigrator.MigrateAsync(connection, Ct);

        Assert.Single(await library.Store.ListAsync(Ct));
    }
}
