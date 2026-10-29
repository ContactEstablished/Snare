using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Snare.Capture.Scrolling;
using Snare.Core.Imaging;

namespace Snare.App.Scrolling;

/// <summary>
/// The panel beside a scrolling capture: Start, Auto-scroll, Stop, Cancel, and the live preview.
/// Actions go to <see cref="ScrollCaptureService"/> through the delegates it passes in.
/// </summary>
public sealed partial class ScrollPanelViewModel(
    Func<Task> startManual,
    Func<Task> startAutomatic,
    Func<Task> stop,
    Func<Task> cancel) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HintText))]
    [NotifyCanExecuteChangedFor(nameof(StartManualCommand), nameof(StartAutomaticCommand), nameof(StopCommand), nameof(CancelCommand))]
    public partial ScrollPanelState State { get; private set; }

    [ObservableProperty]
    public partial string HeightText { get; private set; } = "";

    [ObservableProperty]
    public partial string? Warning { get; private set; }

    /// <summary>The bottom of the image so far; the view draws it.</summary>
    [ObservableProperty]
    public partial PixelBuffer? Preview { get; private set; }

    public string HintText => State switch
    {
        ScrollPanelState.Ready => "Start, then scroll down at your own pace. Auto-scroll scrolls the page for you (Chrome, Edge).",
        ScrollPanelState.Manual => "Scroll down steadily. Stop (or Ctrl+Shift+PrtSc) at the end.",
        ScrollPanelState.Automatic => "Scrolling to the end of the page. Stop to end sooner.",
        _ => "Saving…",
    };

    public void SetState(ScrollPanelState state) => State = state;

    /// <summary>Applies a progress report from the session (on the UI thread).</summary>
    public void Apply(ScrollProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        HeightText = string.Create(CultureInfo.CurrentCulture, $"{progress.Height:N0} px of {progress.MaxHeight:N0}");
        Warning = progress.Outcome == StitchOutcome.LimitReached
            ? "Reached the height limit."
            : progress.Warning;
        if (progress.Tail is { } tail)
        {
            Preview = tail;
        }
    }

    private bool IsReady => State == ScrollPanelState.Ready;

    private bool IsRunning => State is ScrollPanelState.Manual or ScrollPanelState.Automatic;

    private bool CanCancel => State != ScrollPanelState.Finishing;

    [RelayCommand(CanExecute = nameof(IsReady))]
    private Task StartManual() => startManual();

    [RelayCommand(CanExecute = nameof(IsReady))]
    private Task StartAutomatic() => startAutomatic();

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private Task Stop() => stop();

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private Task Cancel() => cancel();
}
