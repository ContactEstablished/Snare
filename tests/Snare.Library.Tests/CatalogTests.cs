using Snare.Core.Captures;
using Snare.Core.Imaging;
using Snare.Core.Search;

namespace Snare.Library.Tests;

public class CatalogTests
{
    private static readonly DateTimeOffset Oct28 = new(2026, 10, 28, 9, 0, 0, TimeSpan.FromHours(-4));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<CaptureRecord> Save(TempLibrary library, string app, string title, DateTimeOffset? at = null) =>
        Save(library, app, title, at ?? Oct28, ocr: null);

    private static async Task<CaptureRecord> Save(TempLibrary library, string app, string? title, DateTimeOffset at, string? ocr)
    {
        var record = await library.Store.SaveImageAsync(PixelBuffer.Filled(4, 4, 0xFF000000), new CaptureSource(app, $"{app} window"), at, Ct);
        if (title is not null)
        {
            record = await library.Store.UpdateDetailsAsync(record.Id, title, null, Ct);
        }

        if (ocr is not null)
        {
            await library.Store.SetOcrTextAsync(record.Id, ocr, Ct);
        }

        return record;
    }

    private static async Task<IReadOnlyList<Guid>> Find(TempLibrary library, string text) =>
        (await library.Store.SearchAsync(SearchQueryParser.Parse(text), Ct)).Select(r => r.Id).ToList();

    [Fact]
    public async Task Search_MatchesPrefixesAcrossTitleOcrAndApp()
    {
        using var library = new TempLibrary();
        var invoice = await Save(library, "chrome", "Invoices for October", Oct28, ocr: null);
        var error = await Save(library, "notepad", null, Oct28.AddMinutes(1), ocr: "Payment error: card declined");
        var other = await Save(library, "slack", "Lunch plans", Oct28.AddMinutes(2), ocr: null);

        Assert.Equal([invoice.Id], await Find(library, "invoice"));
        Assert.Equal([error.Id], await Find(library, "err"));
        Assert.Empty(await Find(library, "invoice err")); // both terms must match
        Assert.Equal([other.Id], await Find(library, "slack"));
        Assert.Equal([other.Id, error.Id, invoice.Id], await Find(library, "")); // newest first
    }

    [Fact]
    public async Task Search_IgnoresCaseAndAccents()
    {
        using var library = new TempLibrary();
        var cafe = await Save(library, "chrome", "Café menu");

        Assert.Equal([cafe.Id], await Find(library, "CAFE"));
    }

    [Fact]
    public async Task Search_RanksTitleAboveOcrText()
    {
        using var library = new TempLibrary();
        var inOcr = await Save(library, "chrome", "Something else", Oct28, ocr: "deploy deploy deploy");
        var inTitle = await Save(library, "chrome", "Deploy checklist", Oct28.AddMinutes(-5), ocr: null);

        Assert.Equal([inTitle.Id, inOcr.Id], await Find(library, "deploy"));
    }

    [Fact]
    public async Task Search_FiltersByTagAppKindAndDates()
    {
        using var library = new TempLibrary();
        var a = await Save(library, "chrome", "A", new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.FromHours(-4)));
        var b = await Save(library, "chromium", "B", new DateTimeOffset(2026, 10, 1, 0, 30, 0, TimeSpan.FromHours(-4)));
        var c = await Save(library, "notepad", "C", new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(-4)));
        await library.Store.SetTagsAsync(a.Id, ["bug", "urgent"], Ct);
        await library.Store.SetTagsAsync(c.Id, ["Bug"], Ct);

        Assert.Equal([c.Id, a.Id], await Find(library, "tag:bug"));
        Assert.Equal([a.Id], await Find(library, "tag:BUG tag:urgent"));
        Assert.Equal([b.Id, a.Id], await Find(library, "app:chrom"));
        Assert.Equal([a.Id], await Find(library, "before:2026-10-01"));
        Assert.Equal([c.Id, b.Id], await Find(library, "after:2026-10-01"));
        Assert.Equal([c.Id, b.Id, a.Id], await Find(library, "kind:image"));
        Assert.Empty(await Find(library, "kind:video"));
    }

    [Fact]
    public async Task Search_ExcludesTheTrash()
    {
        using var library = new TempLibrary();
        var kept = await Save(library, "chrome", "Keep me");
        var trashed = await Save(library, "chrome", "Keep me too", Oct28.AddMinutes(1));
        await library.Store.MoveToTrashAsync(trashed.Id, Ct);

        Assert.Equal([kept.Id], await Find(library, "keep"));
    }

    [Fact]
    public async Task Tags_AreDedupedCaseInsensitivelyAndColored()
    {
        using var library = new TempLibrary();
        var a = await Save(library, "chrome", "A");
        var b = await Save(library, "chrome", "B", Oct28.AddMinutes(1));

        await library.Store.SetTagsAsync(a.Id, [" Bug ", "bug", "design"], Ct);
        await library.Store.SetTagsAsync(b.Id, ["BUG"], Ct);

        Assert.Equal(["Bug", "design"], await library.Store.GetTagsAsync(a.Id, Ct));
        Assert.Equal(["Bug"], await library.Store.GetTagsAsync(b.Id, Ct));
        var tags = await library.Store.ListTagsAsync(Ct);
        Assert.Equal([("Bug", 2), ("design", 1)], tags.Select(t => (t.Name, t.Count)));
        Assert.Equal([TagPalette.ForIndex(0), TagPalette.ForIndex(1)], tags.Select(t => t.Color));
    }

    [Fact]
    public async Task Tags_NoLongerUsed_AreRemoved_AndSearchFollows()
    {
        using var library = new TempLibrary();
        var a = await Save(library, "chrome", "A");
        await library.Store.SetTagsAsync(a.Id, ["temporary"], Ct);
        Assert.Equal([a.Id], await Find(library, "temporary"));

        await library.Store.SetTagsAsync(a.Id, [], Ct);

        Assert.Empty(await library.Store.ListTagsAsync(Ct));
        Assert.Empty(await Find(library, "temporary"));
    }

    [Fact]
    public async Task AddTags_KeepsExistingTagsOnEachCapture()
    {
        using var library = new TempLibrary();
        var a = await Save(library, "chrome", "A");
        var b = await Save(library, "chrome", "B", Oct28.AddMinutes(1));
        await library.Store.SetTagsAsync(a.Id, ["old"], Ct);

        await library.Store.AddTagsAsync([a.Id, b.Id], ["new", "old"], Ct);

        Assert.Equal(["new", "old"], await library.Store.GetTagsAsync(a.Id, Ct));
        Assert.Equal(["new", "old"], await library.Store.GetTagsAsync(b.Id, Ct));
    }

    [Fact]
    public async Task TagColor_CanBeChanged()
    {
        using var library = new TempLibrary();
        var a = await Save(library, "chrome", "A");
        await library.Store.SetTagsAsync(a.Id, ["bug"], Ct);

        await library.Store.SetTagColorAsync("BUG", "#000000", Ct);

        Assert.Equal("#000000", Assert.Single(await library.Store.ListTagsAsync(Ct)).Color);
    }

    [Fact]
    public async Task Details_AreSavedTrimmedAndBlankIsNull()
    {
        using var library = new TempLibrary();
        var a = await Save(library, "chrome", "A");

        var updated = await library.Store.UpdateDetailsAsync(a.Id, "  Title  ", "   ", Ct);

        Assert.Equal("Title", updated.Title);
        Assert.Null(updated.Description);
    }

    [Fact]
    public async Task Ocr_AwaitingListShrinksAsTextIsSet()
    {
        using var library = new TempLibrary();
        var a = await Save(library, "chrome", "A");
        var b = await Save(library, "chrome", "B", Oct28.AddMinutes(1));
        Assert.Equal([b.Id, a.Id], await library.Store.ListAwaitingOcrAsync(Ct));

        await library.Store.SetOcrTextAsync(a.Id, "", Ct);

        Assert.Equal([b.Id], await library.Store.ListAwaitingOcrAsync(Ct));
    }

    [Fact]
    public async Task Trash_ListRestoreAndEmpty()
    {
        using var library = new TempLibrary();
        var keep = await Save(library, "chrome", "Keep");
        var restore = await Save(library, "chrome", "Restore", Oct28.AddMinutes(1));
        var purge = await Save(library, "chrome", "Purge", Oct28.AddMinutes(2));
        await library.Store.SetTagsAsync(purge.Id, ["only-on-purged"], Ct);
        await library.Store.MoveToTrashAsync(restore.Id, Ct);
        await library.Store.MoveToTrashAsync(purge.Id, Ct);

        Assert.Equal(2, (await library.Store.ListTrashAsync(Ct)).Count);
        await library.Store.RestoreAsync(restore.Id, Ct);

        var removed = await library.Store.EmptyTrashAsync(Ct);

        Assert.Equal(1, removed);
        Assert.Null(await library.Store.GetAsync(purge.Id, Ct));
        Assert.False(File.Exists(purge.FilePath));
        Assert.False(File.Exists(purge.OriginalPath));
        Assert.False(File.Exists(library.Store.ThumbnailPathFor(purge.Id)));
        Assert.Empty(await library.Store.ListTagsAsync(Ct));
        Assert.Equal([restore.Id, keep.Id], (await library.Store.ListAsync(Ct)).Select(r => r.Id));
        Assert.Empty(await Find(library, "purge"));
    }

    [Fact]
    public async Task UpgradingAPhaseOneDatabase_BackfillsTheSearchIndex()
    {
        using var library = new TempLibrary();
        Directory.CreateDirectory(library.Paths.AppDataRoot);
        using (var connection = library.OpenDatabase())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = Snare.Library.Data.SchemaMigrator.Scripts[0] + """
                ;PRAGMA user_version = 1;
                INSERT INTO Capture (Id, Kind, FilePath, Width, Height, CreatedUtc, ModifiedUtc, Title, IsDeleted)
                VALUES ('0192F2A0-0000-7000-8000-000000000001', 'Image', 'C:\x.png', 4, 4,
                        '2026-10-28 13:00:00', '2026-10-28 13:00:00', 'Quarterly report', 0);
                """;
            command.ExecuteNonQuery();
        }

        var found = await Find(library, "quarter");

        Assert.Equal([Guid.Parse("0192F2A0-0000-7000-8000-000000000001")], found);
    }

    [Fact]
    public async Task ImageChanged_FiresOnSaveAndReexport()
    {
        using var library = new TempLibrary();
        var fired = new List<Guid>();
        library.Store.ImageChanged += (_, id) => fired.Add(id);

        var a = await Save(library, "chrome", "A");
        await library.Store.ReexportAsync(a.Id, new Snare.Core.Annotations.AnnotationDocument { Canvas = new(4, 4) }, Ct);

        Assert.Equal([a.Id, a.Id], fired);
    }
}
