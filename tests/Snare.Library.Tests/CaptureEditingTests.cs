using SkiaSharp;
using Snare.Core.Annotations;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Imaging;
using Snare.Rendering;

namespace Snare.Library.Tests;

public class CaptureEditingTests
{
    private static readonly DateTimeOffset CapturedAt = new(2026, 10, 29, 10, 0, 0, TimeSpan.FromHours(-4));
    private static readonly CaptureSource Source = new("notepad", "Notes");
    private static readonly PixelRect Secret = new(4, 4, 8, 6);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PixelBuffer Image() => PixelBuffer.Filled(40, 30, 0xFF3366CC);

    private static AnnotationDocument Redacted(CanvasInfo? canvas = null) => new()
    {
        Canvas = canvas ?? new CanvasInfo(40, 30),
        Objects = [new RedactAnnotation("r", Secret, RedactMode.Solid)],
    };

    [Fact]
    public async Task LoadOriginal_ReturnsTheCapturedPixels()
    {
        using var library = new TempLibrary();
        var record = await library.Store.SaveImageAsync(Image(), Source, CapturedAt, Ct);

        var original = await library.Store.LoadOriginalAsync(record.Id, Ct);

        Assert.Equal((40, 30), (original.Width, original.Height));
        Assert.Equal(0xFF3366CCu, original.GetArgb(10, 10));
    }

    [Fact]
    public async Task SaveAnnotations_StoresJsonWithoutTouchingTheExport()
    {
        using var library = new TempLibrary();
        var record = await library.Store.SaveImageAsync(Image(), Source, CapturedAt, Ct);
        var exportBefore = await File.ReadAllBytesAsync(record.FilePath, Ct);

        await library.Store.SaveAnnotationsAsync(record.Id, Redacted(), Ct);

        var stored = await library.Store.GetAsync(record.Id, Ct);
        Assert.Equal(Redacted().Objects, AnnotationJson.Deserialize(stored!.AnnotationJson!).Objects);
        Assert.Equal(exportBefore, await File.ReadAllBytesAsync(record.FilePath, Ct));
    }

    [Fact]
    public async Task Reexport_OverwritesTheExportInPlace_AndKeepsTheOriginal()
    {
        using var library = new TempLibrary();
        var record = await library.Store.SaveImageAsync(Image(), Source, CapturedAt, Ct);

        var updated = await library.Store.ReexportAsync(record.Id, Redacted(), Ct);

        Assert.Equal(record.FilePath, updated.FilePath);
        using var export = ImageCodec.Decode(await File.ReadAllBytesAsync(updated.FilePath, Ct));
        Assert.Equal(SKColors.Black, export.GetPixel(5, 5));
        Assert.Equal(new SKColor(0x33, 0x66, 0xCC), export.GetPixel(30, 20));
        var original = await library.Store.LoadOriginalAsync(record.Id, Ct);
        Assert.Equal(0xFF3366CCu, original.GetArgb(5, 5)); // the original is still untouched
        Assert.NotNull(updated.AnnotationJson);
    }

    [Fact]
    public async Task Reexport_WithCropAndResize_UpdatesTheSize()
    {
        using var library = new TempLibrary();
        var record = await library.Store.SaveImageAsync(Image(), Source, CapturedAt, Ct);

        var updated = await library.Store.ReexportAsync(
            record.Id, Redacted(new CanvasInfo(40, 30, new PixelRect(0, 0, 20, 20), new PixelSize(10, 10))), Ct);

        Assert.Equal((10, 10), (updated.Width, updated.Height));
        using var export = ImageCodec.Decode(await File.ReadAllBytesAsync(updated.FilePath, Ct));
        Assert.Equal((10, 10), (export.Width, export.Height));
    }

    [Fact]
    public async Task DiscardOriginal_LeavesNoSecretPixelsAnywhere()
    {
        using var library = new TempLibrary();
        var record = await library.Store.SaveImageAsync(Image(), Source, CapturedAt, Ct);

        var discarded = await library.Store.DiscardOriginalAsync(record.Id, Redacted(), Ct);

        Assert.Null(discarded.OriginalPath);
        Assert.Null(discarded.AnnotationJson);
        Assert.False(File.Exists(record.OriginalPath));

        // Editing now starts from the export, which has the redaction baked in.
        var pixels = await library.Store.LoadOriginalAsync(record.Id, Ct);
        for (var y = Secret.Y; y < Secret.Bottom; y++)
        {
            for (var x = Secret.X; x < Secret.Right; x++)
            {
                Assert.Equal(0xFF000000u, pixels.GetArgb(x, y));
            }
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => library.Store.ReexportAsync(record.Id, Redacted(), Ct));
    }

    [Fact]
    public async Task DiscardOriginal_WhenTheDeleteFails_CanBeRetried()
    {
        using var library = new TempLibrary();
        var record = await library.Store.SaveImageAsync(Image(), Source, CapturedAt, Ct);

        // An open handle without delete sharing makes File.Delete fail, like an indexer lock.
        await using (new FileStream(record.OriginalPath!, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsAsync<IOException>(() => library.Store.DiscardOriginalAsync(record.Id, Redacted(), Ct));
            Assert.Equal(record.OriginalPath, (await library.Store.GetAsync(record.Id, Ct))!.OriginalPath);
        }

        var retried = await library.Store.DiscardOriginalAsync(record.Id, Redacted(), Ct);

        Assert.Null(retried.OriginalPath);
        Assert.False(File.Exists(record.OriginalPath));
    }

    [Fact]
    public async Task DiscardOriginal_FinishesWhenOnlyTheRowUpdateWasLost()
    {
        using var library = new TempLibrary();
        var record = await library.Store.SaveImageAsync(Image(), Source, CapturedAt, Ct);
        await library.Store.ReexportAsync(record.Id, Redacted(), Ct);
        File.Delete(record.OriginalPath!); // as if a previous discard crashed after deleting

        var discarded = await library.Store.DiscardOriginalAsync(record.Id, Redacted(), Ct);

        Assert.Null(discarded.OriginalPath);
    }

    [Fact]
    public async Task MissingOriginal_IsNotSilentlyReplacedByTheExport()
    {
        using var library = new TempLibrary();
        var record = await library.Store.SaveImageAsync(Image(), Source, CapturedAt, Ct);
        File.Delete(record.OriginalPath!);

        await Assert.ThrowsAsync<FileNotFoundException>(() => library.Store.LoadOriginalAsync(record.Id, Ct));
        await Assert.ThrowsAsync<FileNotFoundException>(() => library.Store.ReexportAsync(record.Id, Redacted(), Ct));
    }

    [Fact]
    public async Task SaveAnnotations_AfterADiscard_IsIgnored()
    {
        using var library = new TempLibrary();
        var record = await library.Store.SaveImageAsync(Image(), Source, CapturedAt, Ct);
        await library.Store.DiscardOriginalAsync(record.Id, Redacted(), Ct);

        await library.Store.SaveAnnotationsAsync(record.Id, Redacted(), Ct);

        Assert.Null((await library.Store.GetAsync(record.Id, Ct))!.AnnotationJson);
    }

    [Fact]
    public async Task UnknownCapture_Throws()
    {
        using var library = new TempLibrary();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => library.Store.LoadOriginalAsync(Guid.NewGuid(), Ct));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => library.Store.SaveAnnotationsAsync(Guid.NewGuid(), Redacted(), Ct));
    }

    [Theory]
    [InlineData("a.png", Snare.Core.Options.ExportFormat.Png)]
    [InlineData("a.JPG", Snare.Core.Options.ExportFormat.Jpeg)]
    [InlineData("a.jpeg", Snare.Core.Options.ExportFormat.Jpeg)]
    public void ExportEncoder_FormatComesFromTheExtension(string path, Snare.Core.Options.ExportFormat expected)
    {
        Assert.Equal(expected, ExportEncoder.FormatOf(path));
    }
}
