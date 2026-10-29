using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Snare.App.Recording;

/// <summary>
/// The floating control bar while recording. Actions go to <see cref="RecordingService"/> through
/// the delegates it passes in; the elapsed time excludes paused stretches.
/// </summary>
public sealed partial class RecordingControlViewModel(
    bool hasMicrophone,
    bool hasWebcam,
    Action<bool> setPaused,
    Action<bool> setMicMuted,
    Action<bool> setWebcamVisible,
    Func<Task> stop,
    Func<Task> restart,
    Func<Task> discard) : ObservableObject
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();

    public bool HasMicrophone { get; } = hasMicrophone;

    public bool HasWebcam { get; } = hasWebcam;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseLabel))]
    public partial bool IsPaused { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MicLabel))]
    public partial bool IsMicMuted { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WebcamLabel))]
    public partial bool IsWebcamHidden { get; private set; }

    [ObservableProperty]
    public partial string ElapsedText { get; private set; } = "0:00";

    public string PauseLabel => IsPaused ? "▶ Resume" : "❚❚ Pause";

    public string MicLabel => IsMicMuted ? "Unmute mic" : "Mute mic";

    public string WebcamLabel => IsWebcamHidden ? "Show webcam" : "Hide webcam";

    public TimeSpan Elapsed => _elapsed.Elapsed;

    /// <summary>Called by the view's timer.</summary>
    public void Tick()
    {
        var t = _elapsed.Elapsed;
        ElapsedText = t.TotalHours >= 1
            ? t.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : t.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    /// <summary>After a restart the clock starts again.</summary>
    public void ResetClock()
    {
        IsPaused = false;
        _elapsed.Restart();
        Tick();
    }

    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
        if (IsPaused)
        {
            _elapsed.Stop();
        }
        else
        {
            _elapsed.Start();
        }

        setPaused(IsPaused);
    }

    [RelayCommand]
    private void ToggleMic()
    {
        IsMicMuted = !IsMicMuted;
        setMicMuted(IsMicMuted);
    }

    [RelayCommand]
    private void ToggleWebcam()
    {
        IsWebcamHidden = !IsWebcamHidden;
        setWebcamVisible(!IsWebcamHidden);
    }

    [RelayCommand]
    private Task StopAsync() => stop();

    [RelayCommand]
    private Task RestartAsync() => restart();

    [RelayCommand]
    private Task DiscardAsync() => discard();
}
