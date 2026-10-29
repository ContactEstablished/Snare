using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Snare.Capture.Selection;
using Snare.Core.Captures;
using Snare.Core.Geometry;

namespace Snare.App.Capture;

/// <summary>
/// State shared by every monitor's overlay. All geometry is physical pixels; the views convert
/// to DIPs only to draw. Selection logic lives in <see cref="SelectionSession"/>.
/// </summary>
public sealed partial class CaptureOverlayViewModel : ObservableObject
{
    private readonly TaskCompletionSource<SelectionResult?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public CaptureOverlayViewModel(FrozenDesktop desktop)
    {
        Desktop = desktop;
        Session = new SelectionSession(desktop);
        Session.Changed += (_, _) => OnSessionChanged();
    }

    public FrozenDesktop Desktop { get; }

    public SelectionSession Session { get; }

    /// <summary>Completes with the accepted selection, or null when cancelled.</summary>
    public Task<SelectionResult?> Completion => _completion.Task;

    /// <summary>Raised after any change; overlays redraw on it.</summary>
    public event EventHandler? Redraw;

    public string PositionText => string.Create(CultureInfo.InvariantCulture, $"{Session.Pointer.X}, {Session.Pointer.Y}");

    public string ColorText => Desktop.GetArgb(Session.Pointer) is { } argb
        ? string.Create(CultureInfo.InvariantCulture, $"#{argb & 0xFFFFFF:X6}")
        : "—";

    public string? SizeText => Session.Highlight is { } r
        ? string.Create(CultureInfo.InvariantCulture, $"{r.Width} × {r.Height}")
        : null;

    public void PointerMove(PixelPoint p) => Session.PointerMove(p);

    public void PointerDown(PixelPoint p) => Session.PointerDown(p);

    public void PointerUp(PixelPoint p) => Session.PointerUp(p);

    public void DoubleClick(PixelPoint p) => Session.DoubleClick(p);

    public void Nudge(int x, int y, bool large) => Session.Nudge(x, y, large);

    [RelayCommand]
    private void Accept() => Session.Accept();

    [RelayCommand]
    private void Cancel() => Session.Cancel();

    [RelayCommand]
    private void TakeMonitor() => Session.TakeMonitorAtPointer();

    [RelayCommand]
    private void TakeAllMonitors() => Session.TakeAllMonitors();

    private void OnSessionChanged()
    {
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(ColorText));
        OnPropertyChanged(nameof(SizeText));
        Redraw?.Invoke(this, EventArgs.Empty);

        if (Session.IsFinished)
        {
            _completion.TrySetResult(Session.Result);
        }
    }
}
