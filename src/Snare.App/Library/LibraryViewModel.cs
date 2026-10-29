using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Snare.App.Editor;
using Snare.App.Messages;
using Snare.App.Services;
using Snare.Core.Captures;
using Snare.Core.Search;

namespace Snare.App.Library;

/// <summary>
/// The library: a thumbnail grid filtered by one search box, a detail pane for the selection,
/// bulk tagging and deleting, and the Trash.
/// </summary>
public sealed partial class LibraryViewModel :
    ObservableObject, IRecipient<CaptureSavedMessage>, IRecipient<CaptureTrashedMessage>, IRecipient<CaptureUpdatedMessage>
{
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(250);

    private readonly ICaptureStore _store;
    private readonly ICaptureCatalog _catalog;
    private readonly ClipboardService _clipboard;
    private readonly EditorService _editor;
    private readonly ILogger<LibraryViewModel> _logger;
    private CancellationTokenSource? _pendingSearch;
    private int _loadVersion;

    public LibraryViewModel(
        ICaptureStore store,
        ICaptureCatalog catalog,
        ClipboardService clipboard,
        EditorService editor,
        ILogger<LibraryViewModel> logger)
    {
        _store = store;
        _catalog = catalog;
        _clipboard = clipboard;
        _editor = editor;
        _logger = logger;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ObservableCollection<CaptureTileViewModel> Tiles { get; } = [];

    /// <summary>Set by the view: asks before emptying the Trash.</summary>
    public Func<int, bool>? ConfirmEmptyTrash { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial bool ShowTrash { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSingleSelection), nameof(HasMultipleSelection), nameof(SelectionText))]
    public partial IReadOnlyList<CaptureTileViewModel> Selected { get; private set; } = [];

    [ObservableProperty]
    public partial CaptureDetailViewModel? Detail { get; private set; }

    [ObservableProperty]
    public partial string BulkTags { get; set; } = "";

    public bool HasSingleSelection => Selected.Count == 1;

    public bool HasMultipleSelection => Selected.Count > 1;

    public string SelectionText => string.Create(CultureInfo.CurrentCulture, $"{Selected.Count} selected");

    public string Summary
    {
        get
        {
            if (IsLoading)
            {
                return "Loading…";
            }

            var noun = ShowTrash ? "in the Trash" : !string.IsNullOrWhiteSpace(SearchText) ? (Tiles.Count == 1 ? "match" : "matches") : (Tiles.Count == 1 ? "capture" : "captures");
            return string.Create(CultureInfo.CurrentCulture, $"{Tiles.Count:N0} {noun}");
        }
    }

    public static string SearchHint => "Search titles, tags, text in images…  Filters: tag:bug  app:chrome  kind:image  before:2026-10-01  after:2026-09-01";

    [RelayCommand]
    public async Task LoadAsync()
    {
        var version = ++_loadVersion;
        IsLoading = true;
        IReadOnlyList<CaptureRecord> captures;
        try
        {
            captures = ShowTrash
                ? await _catalog.ListTrashAsync()
                : await _catalog.SearchAsync(SearchQueryParser.Parse(SearchText));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load the library");
            captures = [];
        }

        if (version != _loadVersion)
        {
            return; // a newer search started while this one ran
        }

        Tiles.Clear();
        foreach (var capture in captures)
        {
            Tiles.Add(new CaptureTileViewModel(capture));
        }

        IsLoading = false;
        UpdateSelection([]);

        // Thumbnails load after the grid appears, one at a time, in order.
        foreach (var tile in Tiles.ToList())
        {
            if (version != _loadVersion)
            {
                return;
            }

            await tile.LoadThumbnailAsync(_store.ThumbnailPathFor(tile.Capture.Id));
        }
    }

    /// <summary>Called by the view when the grid's selection changes.</summary>
    public void UpdateSelection(IReadOnlyList<CaptureTileViewModel> selected)
    {
        Selected = selected;
        if (selected.Count == 1 && !ShowTrash)
        {
            var detail = new CaptureDetailViewModel(selected[0].Capture, _catalog, _logger, OnDetailsSaved);
            Detail = detail;
            _ = detail.LoadAsync();
        }
        else
        {
            Detail = null;
        }
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = "";

    partial void OnSearchTextChanged(string value) => ScheduleSearch();

    partial void OnShowTrashChanged(bool value) => _ = LoadAsync();

    /// <summary>Refreshes 250 ms after typing stops. Runs on the UI thread.</summary>
    private async void ScheduleSearch()
    {
        _pendingSearch?.Cancel();
        var cts = _pendingSearch = new CancellationTokenSource();
        try
        {
            await Task.Delay(SearchDelay, cts.Token);
            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
            // typing continued
        }
    }

    public void Receive(CaptureSavedMessage message)
    {
        if (ShowTrash || !string.IsNullOrWhiteSpace(SearchText))
        {
            return; // a new capture can't be in the Trash, and may not match the search
        }

        var tile = new CaptureTileViewModel(message.Capture);
        Tiles.Insert(0, tile);
        OnPropertyChanged(nameof(Summary));
        _ = tile.LoadThumbnailAsync(_store.ThumbnailPathFor(tile.Capture.Id));
    }

    public void Receive(CaptureUpdatedMessage message)
    {
        if (Tiles.FirstOrDefault(t => t.Capture.Id == message.Capture.Id) is { } tile)
        {
            tile.Capture = message.Capture;
            _ = tile.LoadThumbnailAsync(_store.ThumbnailPathFor(tile.Capture.Id));
        }
    }

    public void Receive(CaptureTrashedMessage message)
    {
        if (!ShowTrash && Tiles.FirstOrDefault(t => t.Capture.Id == message.CaptureId) is { } tile)
        {
            Tiles.Remove(tile);
            OnPropertyChanged(nameof(Summary));
        }
    }

    public void Detach()
    {
        _pendingSearch?.Cancel();
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private void OnDetailsSaved(CaptureRecord record)
    {
        if (Tiles.FirstOrDefault(t => t.Capture.Id == record.Id) is { } tile)
        {
            tile.Capture = record;
        }
    }

    // ---- Per-tile commands ----------------------------------------------------------------

    [RelayCommand]
    private async Task EditAsync(CaptureTileViewModel? tile)
    {
        if (tile is not null && !ShowTrash)
        {
            await _editor.OpenAsync(tile.Capture.Id);
        }
    }

    [RelayCommand]
    private static void Open(CaptureTileViewModel? tile)
    {
        if (tile is not null)
        {
            ShellFiles.Open(tile.Capture.FilePath);
        }
    }

    [RelayCommand]
    private async Task CopyAsync(CaptureTileViewModel? tile)
    {
        if (tile is not null)
        {
            await _clipboard.CopyFileAsync(tile.Capture.FilePath);
        }
    }

    [RelayCommand]
    private static void Reveal(CaptureTileViewModel? tile)
    {
        if (tile is not null)
        {
            ShellFiles.Reveal(tile.Capture.FilePath);
        }
    }

    /// <summary>Moves the given tile, or every selected tile, to the Trash.</summary>
    [RelayCommand]
    private async Task TrashAsync(CaptureTileViewModel? tile)
    {
        var targets = tile is not null && !Selected.Contains(tile) ? [tile] : Selected.ToList();
        foreach (var target in targets)
        {
            try
            {
                await _store.MoveToTrashAsync(target.Capture.Id);
                WeakReferenceMessenger.Default.Send(new CaptureTrashedMessage(target.Capture.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not move capture {Id} to the Trash", target.Capture.Id);
            }
        }

        UpdateSelection([]);
    }

    [RelayCommand]
    private async Task AddBulkTagsAsync()
    {
        var names = CaptureDetailViewModel.SplitTags(BulkTags);
        if (names.Count == 0 || Selected.Count == 0)
        {
            return;
        }

        try
        {
            await _catalog.AddTagsAsync(Selected.Select(t => t.Capture.Id).ToList(), names);
            BulkTags = "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not tag {Count} captures", Selected.Count);
        }
    }

    // ---- Trash -------------------------------------------------------------------------------

    [RelayCommand]
    private async Task RestoreAsync(CaptureTileViewModel? tile)
    {
        var targets = tile is not null && !Selected.Contains(tile) ? [tile] : Selected.ToList();
        foreach (var target in targets)
        {
            await _catalog.RestoreAsync(target.Capture.Id);
            Tiles.Remove(target);
        }

        OnPropertyChanged(nameof(Summary));
        UpdateSelection([]);
    }

    [RelayCommand]
    private async Task EmptyTrashAsync()
    {
        var count = ShowTrash ? Tiles.Count : (await _catalog.ListTrashAsync()).Count;
        if (count == 0 || ConfirmEmptyTrash?.Invoke(count) != true)
        {
            return;
        }

        try
        {
            await _catalog.EmptyTrashAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not empty the Trash");
        }

        await LoadAsync();
    }
}
