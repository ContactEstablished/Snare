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

namespace Snare.App.Library;

/// <summary>The library grid: every capture, newest first. Phase 3 adds search, filters and details.</summary>
public sealed partial class LibraryViewModel :
    ObservableObject, IRecipient<CaptureSavedMessage>, IRecipient<CaptureTrashedMessage>, IRecipient<CaptureUpdatedMessage>
{
    private readonly ICaptureStore _store;
    private readonly ClipboardService _clipboard;
    private readonly EditorService _editor;
    private readonly ILogger<LibraryViewModel> _logger;

    public LibraryViewModel(ICaptureStore store, ClipboardService clipboard, EditorService editor, ILogger<LibraryViewModel> logger)
    {
        _store = store;
        _clipboard = clipboard;
        _editor = editor;
        _logger = logger;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ObservableCollection<CaptureTileViewModel> Tiles { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial bool IsLoading { get; set; }

    public string Summary => IsLoading
        ? "Loading…"
        : Tiles.Count == 1 ? "1 capture" : string.Create(CultureInfo.CurrentCulture, $"{Tiles.Count:N0} captures");

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var captures = await _store.ListAsync();
            Tiles.Clear();
            foreach (var capture in captures)
            {
                Tiles.Add(new CaptureTileViewModel(capture));
            }
        }
        finally
        {
            IsLoading = false;
        }

        // Thumbnails load after the grid appears, one at a time, newest first.
        foreach (var tile in Tiles.ToList())
        {
            await tile.LoadThumbnailAsync(_store.ThumbnailPathFor(tile.Capture.Id));
        }
    }

    public void Receive(CaptureSavedMessage message)
    {
        var tile = new CaptureTileViewModel(message.Capture);
        Tiles.Insert(0, tile);
        OnPropertyChanged(nameof(Summary));
        _ = tile.LoadThumbnailAsync(_store.ThumbnailPathFor(tile.Capture.Id));
    }

    public void Receive(CaptureUpdatedMessage message)
    {
        var index = Tiles.ToList().FindIndex(t => t.Capture.Id == message.Capture.Id);
        if (index < 0)
        {
            return;
        }

        var tile = new CaptureTileViewModel(message.Capture);
        Tiles[index] = tile;
        _ = tile.LoadThumbnailAsync(_store.ThumbnailPathFor(tile.Capture.Id));
    }

    public void Receive(CaptureTrashedMessage message)
    {
        if (Tiles.FirstOrDefault(t => t.Capture.Id == message.CaptureId) is { } tile)
        {
            Tiles.Remove(tile);
            OnPropertyChanged(nameof(Summary));
        }
    }

    public void Detach() => WeakReferenceMessenger.Default.UnregisterAll(this);

    [RelayCommand]
    private async Task EditAsync(CaptureTileViewModel? tile)
    {
        if (tile is not null)
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

    [RelayCommand]
    private async Task TrashAsync(CaptureTileViewModel? tile)
    {
        if (tile is null)
        {
            return;
        }

        try
        {
            await _store.MoveToTrashAsync(tile.Capture.Id);
            WeakReferenceMessenger.Default.Send(new CaptureTrashedMessage(tile.Capture.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not move capture {Id} to the Trash", tile.Capture.Id);
        }
    }
}
