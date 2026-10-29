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

namespace Snare.App.QuickBar;

/// <summary>The small bar shown after a capture. Phase 3 adds the tag box.</summary>
public sealed partial class QuickBarViewModel(
    CaptureRecord capture,
    ClipboardService clipboard,
    ICaptureStore store,
    LibraryWindowService library,
    EditorService editor,
    ILogger logger) : ObservableObject
{
    public CaptureRecord Capture { get; } = capture;

    public string Summary => string.Create(CultureInfo.InvariantCulture, $"{Capture.Width} × {Capture.Height} · copied");

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
