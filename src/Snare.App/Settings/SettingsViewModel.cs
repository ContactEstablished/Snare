using System.ComponentModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Snare.Core.Hotkeys;
using Snare.Core.Options;

namespace Snare.App.Settings;

/// <summary>
/// The Settings window: edits a copy of the options and hands them to <see cref="SettingsService"/>
/// on Save. Backup and folder pickers go through delegates (the service and the view supply them).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SnareOptions _original;
    private readonly Func<SnareOptions, Task> _save;
    private readonly Func<Task<string>> _backUp;
    private readonly Func<DateTimeOffset?> _lastBackup;
    private readonly bool _ready;

    public SettingsViewModel(
        SnareOptions current,
        Func<SnareOptions, Task> save,
        Func<Task<string>> backUp,
        Func<DateTimeOffset?> lastBackup,
        string defaultLibraryRoot,
        string defaultBackupFolder)
    {
        ArgumentNullException.ThrowIfNull(current);
        _original = UserSettingsStore.Copy(current);
        _save = save;
        _backUp = backUp;
        _lastBackup = lastBackup;
        DefaultLibraryRoot = defaultLibraryRoot;
        DefaultBackupFolder = defaultBackupFolder;

        Hotkeys = Enum.GetValues<HotkeyAction>().Select(a => new HotkeyItemViewModel(a, current.Hotkeys.Get(a))).ToList();
        foreach (var item in Hotkeys)
        {
            item.PropertyChanged += (_, _) => Validate();
        }

        LibraryRoot = current.LibraryRoot ?? "";
        OpenEditorAfterCapture = current.AfterCapture == AfterCaptureAction.Editor;
        IncludeCursor = current.IncludeCursor;
        UseJpeg = current.DefaultExportFormat == ExportFormat.Jpeg;
        JpegQuality = current.JpegQuality;
        FfmpegPath = current.FfmpegPath ?? "";
        StartWithWindows = current.StartWithWindows;
        BackupFolder = current.BackupFolder ?? "";
        RefreshLastBackup();
        _ready = true;
        Validate();
    }

    public event EventHandler? CloseRequested;

    public IReadOnlyList<HotkeyItemViewModel> Hotkeys { get; }

    public string DefaultLibraryRoot { get; }

    public string DefaultBackupFolder { get; }

    /// <summary>The view's folder picker: initial folder in, chosen folder out (null when cancelled).</summary>
    public Func<string, string?>? PickFolder { get; set; }

    /// <summary>The view's ffmpeg.exe picker.</summary>
    public Func<string?>? PickFfmpeg { get; set; }

    [ObservableProperty]
    public partial string LibraryRoot { get; set; } = "";

    [ObservableProperty]
    public partial bool OpenEditorAfterCapture { get; set; }

    [ObservableProperty]
    public partial bool IncludeCursor { get; set; }

    [ObservableProperty]
    public partial bool UseJpeg { get; set; }

    [ObservableProperty]
    public partial int JpegQuality { get; set; }

    [ObservableProperty]
    public partial string FfmpegPath { get; set; } = "";

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial string BackupFolder { get; set; } = "";

    [ObservableProperty]
    public partial string LastBackupText { get; private set; } = "";

    [ObservableProperty]
    public partial string? BackupStatus { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackUpNowCommand))]
    public partial bool IsBackingUp { get; private set; }

    /// <summary>Why the last Save failed, or null.</summary>
    [ObservableProperty]
    public partial string? SaveError { get; private set; }

    /// <summary>Why Save is off, or null.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string? Problem { get; private set; }

    /// <summary>The edited settings, everything else as it was.</summary>
    public SnareOptions ToOptions()
    {
        var options = UserSettingsStore.Copy(_original);
        foreach (var item in Hotkeys)
        {
            options.Hotkeys.Set(item.Action, item.Gesture);
        }

        options.LibraryRoot = Blank(LibraryRoot);
        options.AfterCapture = OpenEditorAfterCapture ? AfterCaptureAction.Editor : AfterCaptureAction.QuickBar;
        options.IncludeCursor = IncludeCursor;
        options.DefaultExportFormat = UseJpeg ? ExportFormat.Jpeg : ExportFormat.Png;
        options.JpegQuality = JpegQuality;
        options.FfmpegPath = Blank(FfmpegPath);
        options.StartWithWindows = StartWithWindows;
        options.BackupFolder = Blank(BackupFolder);
        return options;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(LibraryRoot) or nameof(JpegQuality) or nameof(FfmpegPath) or nameof(BackupFolder))
        {
            Validate();
        }
    }

    private void Validate()
    {
        if (!_ready)
        {
            return; // the constructor is still filling the fields in
        }

        var hotkeys = new HotkeyOptions();
        foreach (var item in Hotkeys)
        {
            hotkeys.Set(item.Action, item.Gesture);
        }

        Problem = hotkeys.Problems().FirstOrDefault()
            ?? (JpegQuality is < 50 or > 100 ? "JPG quality must be between 50 and 100." : null)
            ?? FolderProblem(LibraryRoot, "library folder")
            ?? FolderProblem(BackupFolder, "backup folder")
            ?? (FfmpegPath.Trim().Length > 0 && !Path.IsPathFullyQualified(FfmpegPath.Trim()) ? "The FFmpeg path must be a full path." : null);
    }

    private static string? FolderProblem(string folder, string name) =>
        folder.Trim().Length > 0 && !Path.IsPathFullyQualified(folder.Trim()) ? $"The {name} must be a full path, such as D:\\Captures." : null;

    private static string? Blank(string text) => text.Trim().Length == 0 ? null : text.Trim();

    private void RefreshLastBackup()
    {
        LastBackupText = _lastBackup() is { } last
            ? string.Create(CultureInfo.CurrentCulture, $"Last backup: {last.ToLocalTime():g}")
            : "No backups yet.";
    }

    private bool CanSave => Problem is null;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        try
        {
            await _save(ToOptions());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveError = $"Couldn't save the settings: {ex.Message}";
            return; // stay open so nothing typed is lost
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private bool CanBackUp => !IsBackingUp;

    /// <summary>Backs up to the folder already saved in Settings (unsaved edits don't apply yet).</summary>
    [RelayCommand(CanExecute = nameof(CanBackUp))]
    private async Task BackUpNow()
    {
        IsBackingUp = true;
        BackupStatus = "Backing up…";
        try
        {
            BackupStatus = await _backUp();
        }
        finally
        {
            IsBackingUp = false;
            RefreshLastBackup();
        }
    }

    [RelayCommand]
    private void BrowseLibrary()
    {
        if (PickFolder?.Invoke(LibraryRoot.Length > 0 ? LibraryRoot : DefaultLibraryRoot) is { } folder)
        {
            LibraryRoot = folder;
        }
    }

    [RelayCommand]
    private void BrowseBackup()
    {
        if (PickFolder?.Invoke(BackupFolder.Length > 0 ? BackupFolder : DefaultBackupFolder) is { } folder)
        {
            BackupFolder = folder;
        }
    }

    [RelayCommand]
    private void BrowseFfmpeg()
    {
        if (PickFfmpeg?.Invoke() is { } path)
        {
            FfmpegPath = path;
        }
    }
}
