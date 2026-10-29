using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Snare.App.Editor;
using Snare.App.Library;
using Snare.App.Messages;
using Snare.App.Services;
using Snare.Core.Captures;
using Snare.Core.Search;

namespace Snare.App.QuickBar;

/// <summary>The small bar shown after a capture: Edit, Copy, Show in folder, Library, Delete, and a tag box.</summary>
public sealed partial class QuickBarViewModel(
    CaptureRecord capture,
    ClipboardService clipboard,
    ICaptureStore store,
    ICaptureCatalog catalog,
    LibraryWindowService library,
    EditorService editor,
    ILogger logger) : ObservableObject
{
    public CaptureRecord Capture { get; } = capture;

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Create(CultureInfo.InvariantCulture, $"{capture.Width} × {capture.Height} · copied");

    [ObservableProperty]
    public partial string TagText { get; set; } = "";

    /// <summary>Typing a comma adds what came before it, like Enter.</summary>
    partial void OnTagTextChanged(string value)
    {
        if (value.TrimEnd().EndsWith(','))
        {
            AddTagsCommand.Execute(null);
        }
    }

    /// <summary>Enter in the tag box: adds the tags (comma-separated) and keeps the bar open.</summary>
    [RelayCommand]
    private async Task AddTagsAsync()
    {
        var names = CaptureDetailViewModel.SplitTags(TagText);
        if (names.Count == 0)
        {
            return;
        }

        try
        {
            await catalog.AddTagsAsync([Capture.Id], names);
            TagText = "";
            Summary = $"Tagged {string.Join(", ", names)}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not tag capture {Id}", Capture.Id);
            Summary = "Could not add tags";
        }
    }

    /// <summary>Raised when the bar should close.</summary>
    public event EventHandler? CloseRequested;

    [RelayCommand]
    private async Task EditAsync()
    {
        Close();
        await editor.OpenAsync(Capture.Id);
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        await clipboard.CopyFileAsync(Capture.FilePath);
        Close();
    }

    [RelayCommand]
    private void Reveal()
    {
        ShellFiles.Reveal(Capture.FilePath);
        Close();
    }

    [RelayCommand]
    private void OpenLibrary()
    {
        library.Show();
        Close();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        try
        {
            await store.MoveToTrashAsync(Capture.Id);
            WeakReferenceMessenger.Default.Send(new CaptureTrashedMessage(Capture.Id));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not move capture {Id} to the Trash", Capture.Id);
        }

        Close();
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
