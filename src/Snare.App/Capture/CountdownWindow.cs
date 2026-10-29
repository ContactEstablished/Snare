using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Snare.Interop;

namespace Snare.App.Capture;

/// <summary>
/// The delayed-capture countdown near the cursor. Excluded from capture, and closed before
/// the freeze anyway, so it never appears in the screenshot.
/// </summary>
internal sealed class CountdownWindow : Window
{
    private readonly TextBlock _number = new()
    {
        Foreground = Brushes.White,
        FontSize = 40,
        FontWeight = FontWeights.SemiBold,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public CountdownWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Width = 72;
        Height = 72;
        Content = new Border
        {
            Width = 72,
            Height = 72,
            CornerRadius = new CornerRadius(36),
            Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x1F, 0x6F, 0xEB)),
            Child = _number,
        };

        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            CaptureExclusion.ExcludeFromCapture(handle);
            var (x, y) = WindowPlacement.GetCursorPosition();
            WindowPlacement.MoveTopmost(handle, x + 24, y + 24);
        };
    }

    /// <summary>Counts down, one second per number, then closes.</summary>
    public async Task RunAsync(int seconds, CancellationToken cancellationToken = default)
    {
        Show();
        try
        {
            for (var remaining = seconds; remaining > 0; remaining--)
            {
                _number.Text = remaining.ToString(CultureInfo.InvariantCulture);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
        finally
        {
            Close();
        }
    }
}
