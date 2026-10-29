using Snare.Core.Annotations;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Imaging;
using Snare.Rendering;

namespace Snare.Library.Tests;

public class ReconcileTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 8, 0, 0, TimeSpan.FromHours(-4));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NothingChanged_ReportsNothing()
    {
        using var library = new TempLibrary();
        await library.Store.SaveImageAsync(PixelBuffer.Filled(4, 4, 0xFF000000), CaptureSource.Unknown, At, cancellationToken: Ct);

        var result = await library.Store.ReconcileAsync(Ct);

        Assert.False(result.Changed);
    }

    [Fact]
    public async Task MissingExport_IsReexportedFromTheOriginalWithItsAnnotations()
    {
        using var library = new TempLibrary();
        var record = await library.Store.SaveImageAsync(PixelBuffer.Filled(20, 20, 0xFFFFFFFF), CaptureSource.Unknown, At, cancellationToken: Ct);
        await library.Store.SaveAnnotationsAsync(record.Id, new AnnotationDocument
        {
            Canvas = new CanvasInfo(20, 20),
            Objects = [new RedactAnnotation("r", new PixelRect(0, 0, 10, 10), RedactMode.Solid)],
        }, Ct);
        File.Delete(record.FilePath);

        var result = await library.Store.ReconcileAsync(Ct);

        Assert.Equal(new ReconcileResult(1, 0, 0), result);
        using var export = ImageCodec.Decode(await File.ReadAllBytesAsync(record.FilePath, Ct));
        Assert.Equal(SkiaSharp.SKColors.Black, export.GetPixel(2, 2));
    }

    [Fact]
    public async Task MissingExportAndOriginal_MovesToTheTrash()
    {
        using var library = new TempLibrary();
        var record = await library.Store.SaveImageAsync(PixelBuffer.Filled(4, 4, 0xFF000000), CaptureSource.Unknown, At, cancellationToken: Ct);
        File.Delete(record.FilePath);
        File.Delete(record.OriginalPath!);

        var result = await library.Store.ReconcileAsync(Ct);

        Assert.Equal(new ReconcileResult(0, 1, 0), result);
        Assert.True((await library.Store.GetAsync(record.Id, Ct))!.IsDeleted);
    }

    [Fact]
    public async Task AFileDatedBefore1970_IsStillImported()
    {
        using var library = new TempLibrary();
        await library.Store.ListAsync(Ct);
        Directory.CreateDirectory(library.Paths.LibraryRoot);
        var old = Path.Combine(library.Paths.LibraryRoot, "ancient.png");
        using (var bitmap = PixelBufferImages.ToBitmap(PixelBuffer.Filled(4, 4, 0xFF000000)))
        {
            await File.WriteAllBytesAsync(old, ImageCodec.EncodePng(bitmap), Ct);
        }

        File.SetLastWriteTimeUtc(old, new DateTime(1960, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await library.Store.ReconcileAsync(Ct);

        Assert.Equal(1, result.Imported);
    }

    [Fact]
    public async Task UnknownImagesUnderTheLibrary_AreImportedOnce()
    {
        using var library = new TempLibrary();
        await library.Store.ListAsync(Ct); // create the database
        var folder = Path.Combine(library.Paths.LibraryRoot, "2026", "09");
        Directory.CreateDirectory(folder);
        var dropped = Path.Combine(folder, "from-another-tool.png");
        using (var bitmap = PixelBufferImages.ToBitmap(PixelBuffer.Filled(30, 10, 0xFF1F6FEB)))
        {
            await File.WriteAllBytesAsync(dropped, ImageCodec.EncodePng(bitmap), Ct);
        }

        await File.WriteAllTextAsync(Path.Combine(folder, "notes.txt"), "not an image", Ct);
        await File.WriteAllTextAsync(Path.Combine(folder, "broken.png"), "not really a png", Ct);

        var first = await library.Store.ReconcileAsync(Ct);
        var second = await library.Store.ReconcileAsync(Ct);

        Assert.Equal(1, first.Imported);
        Assert.Equal(0, second.Imported);
        var imported = Assert.Single(await library.Store.ListAsync(Ct));
        Assert.Equal(Path.GetFullPath(dropped), imported.FilePath, ignoreCase: true);
        Assert.Equal(("imported", "from-another-tool"), (imported.SourceApp, imported.Title));
        Assert.Equal((30, 10), (imported.Width, imported.Height));
        Assert.True(File.Exists(imported.OriginalPath));
        Assert.True(File.Exists(library.Store.ThumbnailPathFor(imported.Id)));
    }
}
