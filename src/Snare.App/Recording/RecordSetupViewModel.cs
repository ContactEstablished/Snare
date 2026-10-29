using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Snare.Core.Geometry;
using Snare.Core.Recording;
using Snare.Recording;

namespace Snare.App.Recording;

/// <summary>The choices before a recording: mic, system audio, webcam and its placement, frame rate, clicks.</summary>
public sealed partial class RecordSetupViewModel : ObservableObject
{
    public static readonly MediaDevice None = new("", "None", false);

    private readonly TaskCompletionSource<RecordingSettings?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly PixelSize _area;

    public RecordSetupViewModel(PixelSize area, IReadOnlyList<MediaDevice> microphones, IReadOnlyList<MediaDevice> webcams, RecordingSettings? last)
    {
        _area = area;
        Microphones = [None, .. microphones];
        Webcams = [None, .. webcams];

        // Last time's choices when the devices still exist; otherwise the default mic and no webcam.
        SelectedMicrophone = last is null
            ? microphones.FirstOrDefault(m => m.IsDefault) ?? None
            : Microphones.FirstOrDefault(m => m.Id == last.MicrophoneId) ?? None;
        SelectedWebcam = Webcams.FirstOrDefault(w => last?.WebcamId is { } id && w.Id == id) ?? None;
        var settings = last ?? new RecordingSettings();
        SystemAudio = settings.SystemAudio;
        WebcamCorner = settings.WebcamCorner;
        WebcamSizePercent = Math.Round(settings.WebcamSize * 100);
        SixtyFps = settings.FrameRate == 60;
        HighlightClicks = settings.HighlightClicks;
    }

    public ObservableCollection<MediaDevice> Microphones { get; }

    public ObservableCollection<MediaDevice> Webcams { get; }

    [ObservableProperty]
    public partial MediaDevice SelectedMicrophone { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWebcam))]
    public partial MediaDevice SelectedWebcam { get; set; }

    [ObservableProperty]
    public partial bool SystemAudio { get; set; }

    [ObservableProperty]
    public partial WebcamCorner WebcamCorner { get; set; }

    [ObservableProperty]
    public partial double WebcamSizePercent { get; set; }

    [ObservableProperty]
    public partial bool SixtyFps { get; set; }

    [ObservableProperty]
    public partial bool HighlightClicks { get; set; }

    public bool HasWebcam => SelectedWebcam != None;

    public string OutputText
    {
        get
        {
            var output = RecordingFrameSize.Fit(_area);
            var scaled = output.Width != _area.Width ? $" (scaled from {_area.Width} × {_area.Height})" : "";
            return string.Create(CultureInfo.InvariantCulture, $"{output.Width} × {output.Height}{scaled} · H.264 MP4");
        }
    }

    /// <summary>Completes with the chosen settings, or null if cancelled.</summary>
    public Task<RecordingSettings?> Result => _result.Task;

    public RecordingSettings ToSettings() => new RecordingSettings(
        MicrophoneId: SelectedMicrophone == None ? null : SelectedMicrophone.Id,
        SystemAudio: SystemAudio,
        WebcamId: SelectedWebcam == None ? null : SelectedWebcam.Id,
        WebcamCorner: WebcamCorner,
        WebcamSize: WebcamSizePercent / 100,
        FrameRate: SixtyFps ? 60 : 30,
        HighlightClicks: HighlightClicks).Normalized();

    [RelayCommand]
    private void SetCorner(WebcamCorner corner) => WebcamCorner = corner;

    [RelayCommand]
    private void Record() => _result.TrySetResult(ToSettings());

    [RelayCommand]
    public void Cancel() => _result.TrySetResult(null);
}
