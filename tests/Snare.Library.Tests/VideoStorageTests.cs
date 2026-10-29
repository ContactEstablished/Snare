using Snare.Core.Captures;
using Snare.Core.Imaging;
using Snare.Core.Search;
using Snare.Rendering;

namespace Snare.Library.Tests;

public class VideoStorageTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 11, 15, 30, TimeSpan.FromHours(-4));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<string> FakeRecordingAsync(TempLibrary library)
    {
        var path = library.Store.NewVideoPath(At);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, [0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p'], Ct);
        return path;
    }

    private static byte[] Png()
    {
        using var bitmap = PixelBufferImages.ToBitmap(PixelBuffer.Filled(640, 360, 0xFF1F6FEB));
        return ImageCodec.EncodePng(bitmap);
    }

    [Fact]
    public async Task NewVideoPath_IsNamedLikeStillsWithSuffixes()
    {
        using var library = new TempLibrary();

        var first = await FakeRecordingAsync(library);
        var second = library.Store.NewVideoPath(At);

        Assert.EndsWith(Path.Combine("2026", "10", "2026-10-29_111530.mp4"), first, StringComparison.Ordinal);
        Assert.EndsWith("2026-10-29_111530_2.mp4", second, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveVideo_AddsAVideoRowWithThumbnail_AndIsNeverOcrd()
    {
        using var library = new TempLibrary();
        var path = await FakeRecordingAsync(library);

        var record = await library.Store.SaveVideoAsync(path, 4096, 1152, 21_400, new CaptureSource("chrome", "Demo"), At, Png(), Ct);

        Assert.Equal(CaptureKind.Video, record.Kind);
        Assert.Equal((4096, 1152, 21_400L), (record.Width, record.Height, record.DurationMs));
        Assert.Null(record.OriginalPath);
        Assert.True(File.Exists(library.Store.ThumbnailPathFor(record.Id)));
        Assert.Empty(await library.Store.ListAwaitingOcrAsync(Ct));
        Assert.Equal([record.Id], (await library.Store.SearchAsync(SearchQueryParser.Parse("kind:video chrome"), Ct)).Select(r => r.Id));
    }

    [Fact]
    public async Task SaveVideo_WithoutAThumbnail_StillSaves()
    {
        using var library = new TempLibrary();
        var path = await FakeRecordingAsync(library);

        var record = await library.Store.SaveVideoAsync(path, 1280, 720, 1000, CaptureSource.Unknown, At, null, Ct);

        Assert.False(File.Exists(library.Store.ThumbnailPathFor(record.Id)));
        Assert.Single(await library.Store.ListAsync(Ct));
    }

    [Fact]
    public async Task SaveVideo_RequiresTheFile()
    {
        using var library = new TempLibrary();

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            library.Store.SaveVideoAsync(@"C:\nowhere\missing.mp4", 2, 2, 1, CaptureSource.Unknown, At, null, Ct));
    }

    [Fact]
    public async Task UpdateVideo_ChangesDurationAndThumbnail()
    {
        using var library = new TempLibrary();
        var path = await FakeRecordingAsync(library);
        var record = await library.Store.SaveVideoAsync(path, 640, 360, 10_000, CaptureSource.Unknown, At, null, Ct);

        var updated = await library.Store.UpdateVideoAsync(record.Id, 4_000, Png(), Ct);

        Assert.Equal(4_000, updated.DurationMs);
        Assert.True(File.Exists(library.Store.ThumbnailPathFor(record.Id)));
    }

    [Fact]
    public async Task Reconcile_TrashesAVideoWhoseFileIsGone_AndNeverImportsMp4s()
    {
        using var library = new TempLibrary();
        var path = await FakeRecordingAsync(library);
        var record = await library.Store.SaveVideoAsync(path, 640, 360, 10_000, CaptureSource.Unknown, At, null, Ct);
        var stray = Path.Combine(Path.GetDirectoryName(path)!, "stray.mp4");
        File.Copy(path, stray);
        File.Delete(path);

        var result = await library.Store.ReconcileAsync(Ct);

        Assert.Equal(new ReconcileResult(0, 1, 0), result);
        Assert.True((await library.Store.GetAsync(record.Id, Ct))!.IsDeleted);
    }
}
