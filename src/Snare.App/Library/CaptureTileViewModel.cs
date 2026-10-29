using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Snare.Core.Captures;

namespace Snare.App.Library;

/// <summary>One thumbnail in the library grid.</summary>
public sealed partial class CaptureTileViewModel(CaptureRecord capture) : ObservableObject
{
    [ObservableProperty]
    public partial ImageSource? Thumbnail { get; set; }

    public CaptureRecord Capture { get; } = capture;

    public string When => Capture.CreatedUtc.ToLocalTime().ToString("MMM d, yyyy  h:mm tt", CultureInfo.CurrentCulture);

    public string Details => string.Create(CultureInfo.InvariantCulture,
        $"{Capture.Width} × {Capture.Height}{(string.IsNullOrEmpty(Capture.SourceApp) ? "" : $" · {Capture.SourceApp}")}");

    public string ToolTip => Capture.SourceWindowTitle ?? Capture.FilePath;

    public async Task LoadThumbnailAsync(string path) => Thumbnail = await ThumbnailLoader.LoadAsync(path);
}
