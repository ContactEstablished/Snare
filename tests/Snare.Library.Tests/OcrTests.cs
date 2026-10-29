using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Imaging;
using Snare.Core.Search;
using Snare.Library.Ocr;

namespace Snare.Library.Tests;

public class OcrTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Tiling_SmallImageIsOneTile()
    {
        Assert.Equal([new PixelRect(0, 0, 800, 600)], OcrTiling.Plan(800, 600, 10000));
    }

    [Fact]
    public void Tiling_TallImageIsSplitWithOverlapAndCoversEverything()
    {
        var tiles = OcrTiling.Plan(1000, 25000, 10000);

        Assert.Equal(3, tiles.Count);
        Assert.Equal(new PixelRect(0, 0, 1000, 10000), tiles[0]);
        Assert.Equal(10000 - OcrTiling.Overlap, tiles[1].Y);
        Assert.Equal(25000, tiles[^1].Bottom);
        Assert.All(tiles, t => Assert.True(t.Height <= 10000));
    }

    [Fact]
    public void Tiling_WideAndTallMakesAGrid()
    {
        Assert.Equal(4, OcrTiling.Plan(15000, 15000, 10000).Count);
    }

    [Fact]
    public void JoinLines_DropsLinesReadTwiceAtTheSeam()
    {
        var joined = OcrTiling.JoinLines(
        [
            ["Line one", "Line two", "Line three"],
            ["Line two", "Line three", "Line four"],
            ["Line five"],
        ]);

        Assert.Equal("Line one\nLine two\nLine three\nLine four\nLine five", joined);
    }

    [Fact]
    public async Task Maintenance_ReadsTextOfNewAndExistingCaptures()
    {
        using var library = new TempLibrary();
        var existing = await library.Store.SaveImageAsync(PixelBuffer.Filled(8, 8, 0xFF000000), CaptureSource.Unknown, DateTimeOffset.Now, Ct);
        var ocr = new FakeOcr("hello from ocr");
        var service = new LibraryMaintenanceService(library.Store, ocr, new OcrQueue(), NullLogger<LibraryMaintenanceService>.Instance);

        await service.StartAsync(Ct);
        await service.Started;
        var fresh = await library.Store.SaveImageAsync(PixelBuffer.Filled(8, 8, 0xFF000000), CaptureSource.Unknown, DateTimeOffset.Now, Ct);

        await WaitUntil(async () => (await library.Store.ListAwaitingOcrAsync(Ct)).Count == 0);
        await service.StopAsync(Ct);

        Assert.Equal("hello from ocr", (await library.Store.GetAsync(existing.Id, Ct))!.OcrText);
        Assert.Equal("hello from ocr", (await library.Store.GetAsync(fresh.Id, Ct))!.OcrText);
        Assert.Equal([fresh.Id], (await library.Store.SearchAsync(SearchQueryParser.Parse("hello"), Ct)).Take(1).Select(r => r.Id));
    }

    [Fact]
    public async Task Maintenance_SurvivesAnOcrFailure()
    {
        using var library = new TempLibrary();
        var bad = await library.Store.SaveImageAsync(PixelBuffer.Filled(8, 8, 0xFF000000), CaptureSource.Unknown, DateTimeOffset.Now, Ct);
        var ocr = new FakeOcr("ok") { FailFor = 1 };
        var service = new LibraryMaintenanceService(library.Store, ocr, new OcrQueue(), NullLogger<LibraryMaintenanceService>.Instance);

        await service.StartAsync(Ct);
        await service.Started;
        var good = await library.Store.SaveImageAsync(PixelBuffer.Filled(8, 8, 0xFF000000), CaptureSource.Unknown, DateTimeOffset.Now, Ct);
        await WaitUntil(async () => (await library.Store.GetAsync(good.Id, Ct))!.OcrText is not null);
        await service.StopAsync(Ct);

        Assert.Null((await library.Store.GetAsync(bad.Id, Ct))!.OcrText); // retried on the next start
        Assert.Equal("ok", (await library.Store.GetAsync(good.Id, Ct))!.OcrText);
    }

    [Fact]
    public void Queue_DropsDuplicatesWhileQueued()
    {
        var queue = new OcrQueue();
        var id = Guid.NewGuid();

        queue.Enqueue(id);
        queue.Enqueue(id);

        Assert.Equal(1, queue.Count);
    }

    /// <summary>Real Windows OCR; run with Snare.Library.Tests.exe -explicit only.</summary>
    [Fact(Explicit = true)]
    public async Task WindowsOcr_ReadsRenderedText()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(900, 200, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        using (var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 48))
        using (var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true })
        {
            canvas.Clear(SKColors.White);
            canvas.DrawText("Invoice 1234 payment failed", 20, 90, SKTextAlign.Left, font, paint);
            canvas.DrawText("Second line of text", 20, 160, SKTextAlign.Left, font, paint);
        }

        var pixels = new byte[PixelBuffer.ByteCount(900, 200)];
        System.Runtime.InteropServices.Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);

        var text = await new WindowsOcrEngine().RecognizeAsync(new PixelBuffer(900, 200, pixels), Ct);

        TestContext.Current.SendDiagnosticMessage($"OCR: {text.Replace('\n', '|')}");
        Assert.Contains("Invoice", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Second line", text, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WaitUntil(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for background OCR.");
            await Task.Delay(50, Ct);
        }
    }

    private sealed class FakeOcr(string text) : IOcrEngine
    {
        private int _calls;

        public int FailFor { get; init; }

        public Task<string> RecognizeAsync(PixelBuffer image, CancellationToken cancellationToken = default) =>
            Interlocked.Increment(ref _calls) <= FailFor
                ? throw new InvalidOperationException("OCR blew up")
                : Task.FromResult(text);
    }
}
