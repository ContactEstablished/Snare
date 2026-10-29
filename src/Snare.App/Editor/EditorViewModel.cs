using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Snare.App.Messages;
using Snare.App.Services;
using Snare.Core.Annotations;
using Snare.Core.Captures;
using Snare.Core.Imaging;
using Snare.Core.Options;
using Snare.Library;
using Snare.Rendering;
using Snare.Rendering.Editing;

namespace Snare.App.Editor;

/// <summary>
/// The editor window's state and commands. Editing logic lives in <see cref="EditorSession"/>;
/// this adds the style panel, autosave, export, clipboard and discard.
/// </summary>
public sealed partial class EditorViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan AutosaveDelay = TimeSpan.FromSeconds(1.5);

    private readonly ICaptureStore _store;
    private readonly ClipboardService _clipboard;
    private readonly EditorStyleStore _styleStore;
    private readonly AnnotationRenderer _renderer;
    private readonly IOptionsMonitor<SnareOptions> _options;
    private readonly ILogger _logger;
    private CancellationTokenSource? _autosave;
    private Task _autosaveWrite = Task.CompletedTask;
    private string _savedJson;
    private string _exportedJson;

    public EditorViewModel(
        CaptureRecord capture,
        SKBitmap original,
        AnnotationDocument document,
        ICaptureStore store,
        ClipboardService clipboard,
        EditorStyleStore styleStore,
        AnnotationRenderer renderer,
        IOptionsMonitor<SnareOptions> options,
        ILogger logger)
    {
        Capture = capture;
        Original = original;
        OriginalImage = SKImage.FromBitmap(original);
        _store = store;
        _clipboard = clipboard;
        _styleStore = styleStore;
        _renderer = renderer;
        _options = options;
        _logger = logger;

        Session = new EditorSession(original, document, styleStore.Load());
        Session.Changed += OnSessionChanged;
        Session.TextEditRequested += (_, text) => TextEditRequested?.Invoke(this, text);
        _savedJson = _exportedJson = AnnotationJson.Serialize(document);
        RefreshRecentColors();
    }

    public CaptureRecord Capture { get; private set; }

    public SKBitmap Original { get; }

    public SKImage OriginalImage { get; }

    public AnnotationRenderer Renderer => _renderer;

    public EditorSession Session { get; }

    private static readonly IReadOnlyList<string> PaletteColors =
        ["#E53935", "#FB8C00", "#FFEB3B", "#43A047", "#1E88E5", "#8E24AA", "#000000", "#FFFFFF"];

    public IReadOnlyList<string> Palette => PaletteColors;

    public ObservableCollection<string> RecentColors { get; } = [];

    /// <summary>Set by the view: returns a path for Save As, or null if cancelled.</summary>
    public Func<string, string?>? PickSaveAsPath { get; set; }

    /// <summary>Set by the view: asks Matt to confirm discarding the original.</summary>
    public Func<bool>? ConfirmDiscardOriginal { get; set; }

    public event EventHandler<TextAnnotation>? TextEditRequested;

    public event EventHandler? CloseRequested;

    public string Title => $"Snare Editor — {Path.GetFileName(Capture.FilePath)}";

    [ObservableProperty]
    public partial string Status { get; set; } = "Saved";

    [ObservableProperty]
    public partial string ZoomText { get; set; } = "100%";

    public EditorTool Tool
    {
        get => Session.Tool;
        set => Session.Tool = value;
    }

    public bool CanUndo => Session.History.CanUndo;

    public bool CanRedo => Session.History.CanRedo;

    /// <summary>Blur and pixelate obscure; only solid redaction is secure.</summary>
    public bool HasObscuringRedaction => Session.Objects.OfType<RedactAnnotation>().Any(r => r.Mode != RedactMode.Solid);

    public bool CanDiscardOriginal => Capture.OriginalPath is not null;

    /// <summary>The editor suggests discarding the original once something is solidly redacted.</summary>
    public bool SuggestDiscardOriginal => CanDiscardOriginal && Session.Objects.OfType<RedactAnnotation>().Any(r => r.Mode == RedactMode.Solid);

    public bool NeedsExport => AnnotationJson.Serialize(Session.ToDocument()) != _exportedJson;

    // ---- Style panel: edits the selection with the Select tool, else the current tool's style ----

    private EditorTool StyleTool => Tool == EditorTool.Select && Session.SelectedItems.Count > 0
        ? EditorSession.ToolOf(Session.SelectedItems[0])
        : Tool;

    private ToolStyle DisplayedStyle => Tool == EditorTool.Select && Session.SelectedItems.Count > 0
        ? Session.StyleOf(Session.SelectedItems[0])
        : Session.CurrentStyle;

    public string Color
    {
        get => DisplayedStyle.Color;
        set => EditStyle(s => s with { Color = value });
    }

    public double StrokeWidth
    {
        get => DisplayedStyle.Width;
        set => EditStyle(s => s with { Width = (float)Math.Round(value) });
    }

    public bool Filled
    {
        get => DisplayedStyle.FillColor is not null;
        set => EditStyle(s => s with { FillColor = value ? WithAlpha(s.Color, 0x80) : null });
    }

    public double FontSize
    {
        get => DisplayedStyle.FontSize;
        set => EditStyle(s => s with { FontSize = (float)Math.Round(value) });
    }

    public bool Bold
    {
        get => DisplayedStyle.Bold;
        set => EditStyle(s => s with { Bold = value });
    }

    public bool TextBackground
    {
        get => DisplayedStyle.TextBackground is not null;
        set => EditStyle(s => s with { TextBackground = value ? "#DD1F1F1F" : null });
    }

    public double Strength
    {
        get => DisplayedStyle.Strength;
        set => EditStyle(s => s with { Strength = (float)Math.Round(value) });
    }

    public bool OpenArrowHead
    {
        get => DisplayedStyle.HeadStyle == ArrowHeadStyle.Open;
        set => EditStyle(s => s with { HeadStyle = value ? ArrowHeadStyle.Open : ArrowHeadStyle.Filled });
    }

    public bool Dashed
    {
        get => DisplayedStyle.Dashed;
        set => EditStyle(s => s with { Dashed = value });
    }

    public bool ShowsColor => StyleTool is not (EditorTool.Select or EditorTool.Blur or EditorTool.Pixelate or EditorTool.Crop or EditorTool.Eyedropper);

    public bool ShowsWidth => StyleTool is EditorTool.Arrow or EditorTool.Line or EditorTool.Rectangle or EditorTool.Ellipse
        or EditorTool.Pen or EditorTool.Highlighter or EditorTool.Step or EditorTool.Callout;

    public bool ShowsFill => StyleTool is EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Callout;

    public bool ShowsText => StyleTool is EditorTool.Text or EditorTool.Callout;

    public bool ShowsTextBackground => StyleTool is EditorTool.Text;

    public bool ShowsStrength => StyleTool is EditorTool.Blur or EditorTool.Pixelate;

    public bool ShowsArrowHead => StyleTool is EditorTool.Arrow;

    public bool ShowsDashed => StyleTool is EditorTool.Line;

    public bool IsCropping => Tool == EditorTool.Crop;

    public bool HasCrop => Session.Document.Canvas.Crop is not null;

    public string SizeText
    {
        get
        {
            var size = Session.Document.Canvas.OutputSize;
            return string.Create(CultureInfo.InvariantCulture, $"{size.Width} × {size.Height}");
        }
    }

    // ---- Commands -----------------------------------------------------------------------

    [RelayCommand]
    private void SetTool(EditorTool tool) => Tool = tool;

    [RelayCommand]
    private void SetColor(string color) => Color = color;

    [RelayCommand]
    private void Undo() => Session.Undo();

    [RelayCommand]
    private void Redo() => Session.Redo();

    [RelayCommand]
    private void Delete() => Session.DeleteSelection();

    [RelayCommand]
    private void BringToFront() => Session.BringToFront();

    [RelayCommand]
    private void SendToBack() => Session.SendToBack();

    [RelayCommand]
    private void ApplyCrop() => Session.ApplyCrop();

    [RelayCommand]
    private void ClearCrop() => Session.ClearCrop();

    /// <summary>Copies the flattened image, exactly as it would export.</summary>
    [RelayCommand]
    private async Task CopyImageAsync()
    {
        var document = Session.ToDocument();
        var (buffer, png) = await Task.Run(() =>
        {
            using var flattened = _renderer.Render(Original, document);
            return (ToPixelBuffer(flattened), ImageCodec.EncodePng(flattened));
        });
        Status = await _clipboard.CopyAsync(buffer, png) ? "Copied" : "Clipboard busy";
    }

    /// <summary>
    /// Ctrl+S: re-exports if anything changed since the last export. Returns false if the
    /// export failed; the edits are then still kept as annotation JSON when possible.
    /// </summary>
    [RelayCommand]
    public async Task<bool> SaveAsync()
    {
        CancelAutosave();
        await WaitForAutosaveWriteAsync();

        var document = Session.ToDocument();
        var json = AnnotationJson.Serialize(document);
        if (json == _exportedJson)
        {
            Status = "Saved";
            return true;
        }

        Status = "Saving…";
        try
        {
            Capture = await Task.Run(() => _store.ReexportAsync(Capture.Id, document));
            _savedJson = _exportedJson = json;
            Session.MarkSaved();
            Status = "Saved";
            WeakReferenceMessenger.Default.Send(new CaptureUpdatedMessage(Capture));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not save capture {Id}", Capture.Id);
            Status = $"Save failed: {ex.Message}";
        }

        // The image couldn't be updated; at least keep the edits so reopening shows them.
        try
        {
            if (json != _savedJson)
            {
                await _store.SaveAnnotationsAsync(Capture.Id, document);
                _savedJson = json;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not keep the edits of capture {Id} either", Capture.Id);
        }

        return false;
    }

    /// <summary>An autosave already writing must finish before a re-export, or it could land after it.</summary>
    private async Task WaitForAutosaveWriteAsync()
    {
        try
        {
            await _autosaveWrite;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "The pending autosave failed; the save below supersedes it");
        }
    }

    [RelayCommand]
    private async Task SaveAsAsync()
    {
        var suggested = Path.GetFileNameWithoutExtension(Capture.FilePath);
        if (PickSaveAsPath?.Invoke(suggested) is not { } path)
        {
            return;
        }

        var document = Session.ToDocument();
        var quality = _options.CurrentValue.JpegQuality;
        try
        {
            var bytes = await Task.Run(() => ExportEncoder.Render(_renderer, Original, document, ExportEncoder.FormatOf(path), quality));
            await File.WriteAllBytesAsync(path, bytes);
            Status = $"Saved a copy as {Path.GetFileName(path)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Save As to {Path} failed", path);
            Status = $"Save As failed: {ex.Message}";
        }
    }

    /// <summary>Bakes the edits into the export and deletes the original. Can't be undone; the editor closes.</summary>
    [RelayCommand]
    private async Task DiscardOriginalAsync()
    {
        if (!CanDiscardOriginal || ConfirmDiscardOriginal?.Invoke() != true)
        {
            return;
        }

        CancelAutosave();
        await WaitForAutosaveWriteAsync();
        var document = Session.ToDocument();
        try
        {
            Capture = await Task.Run(() => _store.DiscardOriginalAsync(Capture.Id, document));
            _savedJson = _exportedJson = AnnotationJson.Serialize(document);
            Session.MarkSaved();
            WeakReferenceMessenger.Default.Send(new CaptureUpdatedMessage(Capture));
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not discard the original of {Id}", Capture.Id);
            Status = $"Discard failed: {ex.Message}";
        }
    }

    public void Dispose()
    {
        CancelAutosave();
        Session.Changed -= OnSessionChanged;
        _styleStore.Save(Session.Styles);
        OriginalImage.Dispose();
        Original.Dispose();
    }

    // ---- Internals -------------------------------------------------------------------------

    private void EditStyle(Func<ToolStyle, ToolStyle> change)
    {
        if (Tool == EditorTool.Select && Session.SelectedItems.Count > 0)
        {
            var styled = change(DisplayedStyle);
            Session.ApplyStyleToSelection(change);

            // Selected objects also update their tool's remembered style, as if drawn that way.
            Session.SetStyle(StyleTool, styled);
        }
        else
        {
            Session.SetStyle(Tool, change(Session.CurrentStyle));
        }
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(string.Empty); // the style panel and buttons all derive from the session
        RefreshRecentColors();
        if (Session.HasUnsavedChanges)
        {
            Status = "Edited";
            ScheduleAutosave();
        }
    }

    /// <summary>Saves the annotation JSON a moment after the last change. Runs on the UI thread.</summary>
    private async void ScheduleAutosave()
    {
        CancelAutosave();
        var cts = _autosave = new CancellationTokenSource();
        try
        {
            await Task.Delay(AutosaveDelay, cts.Token);
            var document = Session.ToDocument();
            var json = AnnotationJson.Serialize(document);
            if (json != _savedJson)
            {
                // Not cancellable once started; SaveAsync waits for it instead.
                _autosaveWrite = _store.SaveAnnotationsAsync(Capture.Id, document, CancellationToken.None);
                await _autosaveWrite;
                _savedJson = json;
                Status = NeedsExport ? "Edits kept · Save to update the image" : "Saved";
            }
        }
        catch (OperationCanceledException)
        {
            // A newer change restarted the timer.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Autosave failed for capture {Id}", Capture.Id);
            Status = "Autosave failed";
        }
    }

    private void CancelAutosave()
    {
        _autosave?.Cancel();
        _autosave?.Dispose();
        _autosave = null;
    }

    private void RefreshRecentColors()
    {
        if (RecentColors.SequenceEqual(Session.RecentColors))
        {
            return;
        }

        RecentColors.Clear();
        foreach (var color in Session.RecentColors)
        {
            RecentColors.Add(color);
        }
    }

    private static string WithAlpha(string color, byte alpha) =>
        SkiaColors.ToHex(SkiaColors.Parse(color, SKColors.Yellow).WithAlpha(alpha));

    internal static PixelBuffer ToPixelBuffer(SKBitmap bitmap)
    {
        var pixels = new byte[PixelBuffer.ByteCount(bitmap.Width, bitmap.Height)];
        System.Runtime.InteropServices.Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        return new PixelBuffer(bitmap.Width, bitmap.Height, pixels);
    }
}
