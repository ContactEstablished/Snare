namespace Snare.Core.Options;

/// <summary>User settings, bound from the "Snare" section of appsettings.json.</summary>
public sealed class SnareOptions
{
    public const string SectionName = "Snare";

    /// <summary>Folder for exported captures. Empty means %USERPROFILE%\Pictures\Snare.</summary>
    public string? LibraryRoot { get; set; }

    public ExportFormat DefaultExportFormat { get; set; } = ExportFormat.Png;

    public int JpegQuality { get; set; } = 90;

    public bool IncludeCursor { get; set; }

    public AfterCaptureAction AfterCapture { get; set; } = AfterCaptureAction.QuickBar;

    /// <summary>ffmpeg.exe or its folder; empty means tools/ffmpeg next to the app, then PATH.</summary>
    public string? FfmpegPath { get; set; }
}
