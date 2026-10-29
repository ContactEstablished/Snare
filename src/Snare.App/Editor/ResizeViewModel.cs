using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Snare.Core.Geometry;

namespace Snare.App.Editor;

/// <summary>
/// Editor → Resize…: a new output size by width and height (proportions kept by default) or by
/// percentage of the natural size (after any crop).
/// </summary>
public sealed partial class ResizeViewModel : ObservableObject
{
    public const int MaxSide = 32_767;

    private readonly PixelSize _natural;
    private bool _updating;

    public ResizeViewModel(PixelSize current, PixelSize natural)
    {
        _natural = natural;
        _updating = true;
        Width = current.Width;
        Height = current.Height;
        Percent = Math.Round(100.0 * current.Width / natural.Width);
        _updating = false;
    }

    public event EventHandler<PixelSize?>? Closed;

    public string NaturalText => $"Natural size {_natural.Width} × {_natural.Height}";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial int Width { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial int Height { get; set; }

    [ObservableProperty]
    public partial bool KeepProportions { get; set; } = true;

    [ObservableProperty]
    public partial double Percent { get; set; }

    partial void OnWidthChanged(int value)
    {
        if (!_updating && KeepProportions)
        {
            Sync(() => Height = Scaled(value, _natural.Height, _natural.Width));
        }

        Sync(() => Percent = Math.Round(100.0 * value / _natural.Width));
    }

    partial void OnHeightChanged(int value)
    {
        if (!_updating && KeepProportions)
        {
            Sync(() => Width = Scaled(value, _natural.Width, _natural.Height));
        }

        Sync(() => Percent = Math.Round(100.0 * Width / _natural.Width));
    }

    partial void OnPercentChanged(double value)
    {
        if (!_updating && value > 0)
        {
            Sync(() =>
            {
                Width = Math.Max(1, (int)Math.Round(_natural.Width * value / 100));
                Height = Math.Max(1, (int)Math.Round(_natural.Height * value / 100));
            });
        }
    }

    private bool IsValid => Width is >= 1 and <= MaxSide && Height is >= 1 and <= MaxSide;

    [RelayCommand(CanExecute = nameof(IsValid))]
    private void Apply() => Closed?.Invoke(this, new PixelSize(Width, Height));

    [RelayCommand]
    private void Natural() => Closed?.Invoke(this, _natural);

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(this, null);

    private static int Scaled(int value, int numerator, int denominator) =>
        Math.Clamp((int)Math.Round((double)value * numerator / denominator), 1, MaxSide);

    private void Sync(Action change)
    {
        if (_updating)
        {
            return;
        }

        _updating = true;
        try
        {
            change();
        }
        finally
        {
            _updating = false;
        }
    }
}
