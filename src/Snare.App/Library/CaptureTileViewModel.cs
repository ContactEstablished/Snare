using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Snare.Core.Captures;

namespace Snare.App.Library;

/// <summary>One thumbnail in the library grid. Its record is replaced in place when the capture changes.</summary>
public sealed partial class CaptureTileViewModel(CaptureRecord capture) : ObservableObject
{
    [ObservableProperty]
    public partial ImageSource? Thumbnail { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Caption), nameof(Details), nameof(ToolTip))]
    public partial CaptureRecord Capture { get; set; } = capture;

    public string When => Capture.CreatedUtc.ToLocalTime().ToString("MMM d, yyyy  h:mm tt", CultureInfo.CurrentCulture);

    /// <summary>The title if there is one, else the date.</summary>
    public string Caption => string.IsNullOrWhiteSpace(Capture.Title) ? When : Capture.Title;

    public string Details => string.Create(CultureInfo.InvariantCulture,
        $"{Capture.Width} × {Capture.Height}{(string.IsNullOrEmpty(Capture.SourceApp) ? "" : $" · {Capture.SourceApp}")}");

    public string ToolTip => Capture.SourceWindowTitle ?? Capture.FilePath;

    public async Task LoadThumbnailAsync(string path) => Thumbnail = await ThumbnailLoader.LoadAsync(path);
}
