using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Snare.App.Editor;
using Snare.App.Library;
using Snare.App.QuickBar;
using Snare.App.Services;

namespace Snare.App.Tests;

/// <summary>The library and quick bar view models against an in-memory catalog.</summary>
public class LibrarySearchTests
{
    private static EditorService Editors(FakeCaptureStore store) => new(
        store, new TrayIconService(NullLogger<TrayIconService>.Instance), new ServiceCollection().BuildServiceProvider(), NullLogger<EditorService>.Instance);

    private static LibraryViewModel Library(FakeCaptureStore store) => new(
        store, store, new ClipboardService(NullLogger<ClipboardService>.Instance), Editors(store), NullLogger<LibraryViewModel>.Instance);

    private static FakeCaptureStore StoreWith(params string[] titles)
    {
        var store = new FakeCaptureStore();
        for (var i = 0; i < titles.Length; i++)
        {
            store.Captures.Add(FakeCaptureStore.Record(i) with { Title = titles[i] });
        }

        return store;
    }

    [Fact]
    public async Task Search_FiltersAfterTheDelayAndCountsMatches()
    {
        var store = StoreWith("Invoice", "Lunch", "Invoice copy");
        var library = Library(store);
        await library.LoadAsync();
        Assert.Equal("3 captures", library.Summary);

        library.SearchText = "invoice";
        await Task.Delay(LibraryViewModel.SearchDelay * 3, TestContext.Current.CancellationToken);

        Assert.Equal(2, library.Tiles.Count);
        Assert.Equal("2 matches", library.Summary);

        library.ClearSearchCommand.Execute(null);
        await Task.Delay(LibraryViewModel.SearchDelay * 3, TestContext.Current.CancellationToken);
        Assert.Equal(3, library.Tiles.Count);
        library.Detach();
    }

    [Fact]
    public async Task Trash_ShowsTrashedAndRestores()
    {
        var store = StoreWith("A", "B");
        store.Trashed.Add(store.Captures[0].Id);
        var library = Library(store);

        library.ShowTrash = true;
        await library.LoadAsync();
        Assert.Single(library.Tiles);
        Assert.Equal("1 in the Trash", library.Summary);

        library.UpdateSelection([library.Tiles[0]]);
        await library.RestoreCommand.ExecuteAsync(null);

        Assert.Empty(library.Tiles);
        Assert.Empty(store.Trashed);
        library.Detach();
    }

    [Fact]
    public async Task SingleSelection_ShowsDetailsThatSaveTitleAndTags()
    {
        var store = StoreWith("Old title");
        var library = Library(store);
        await library.LoadAsync();

        library.UpdateSelection([library.Tiles[0]]);
        var detail = Assert.IsType<CaptureDetailViewModel>(library.Detail);
        await detail.LoadAsync();

        detail.Title = "New title";
        await detail.SaveDetailsAsync();
        detail.NewTag = "bug, Design";
        await detail.AddTagCommand.ExecuteAsync(null);

        Assert.Equal("New title", library.Tiles[0].Caption);
        Assert.Equal(["bug", "Design"], detail.Tags.Select(t => t.Name));
        Assert.Equal("", detail.NewTag);

        await detail.RemoveTagCommand.ExecuteAsync(detail.Tags[0]);
        Assert.Equal(["Design"], store.TagsById[store.Captures[0].Id]);
        library.Detach();
    }

    [Fact]
    public async Task MultipleSelection_TagsAllAtOnce()
    {
        var store = StoreWith("A", "B", "C");
        var library = Library(store);
        await library.LoadAsync();

        library.UpdateSelection([library.Tiles[0], library.Tiles[1]]);
        Assert.True(library.HasMultipleSelection);
        Assert.Null(library.Detail);
        library.BulkTags = "release";
        await library.AddBulkTagsCommand.ExecuteAsync(null);

        Assert.Equal(2, store.TagsById.Count);
        Assert.All(store.TagsById.Values, tags => Assert.Equal(["release"], tags));
        library.Detach();
    }

    [Fact]
    public async Task QuickBar_TagBoxAddsTagsAndSaysSo()
    {
        var store = StoreWith("A");
        var services = new ServiceCollection().BuildServiceProvider();
        var quickBar = new QuickBarViewModel(
            store.Captures[0], new ClipboardService(NullLogger<ClipboardService>.Instance), store, store,
            new LibraryWindowService(services), Editors(store), NullLogger.Instance);

        quickBar.TagText = "bug, urgent";
        await quickBar.AddTagsCommand.ExecuteAsync(null);

        Assert.Equal(["bug", "urgent"], store.TagsById[store.Captures[0].Id]);
        Assert.Equal("Tagged bug, urgent", quickBar.Summary);
        Assert.Equal("", quickBar.TagText);
    }
}
