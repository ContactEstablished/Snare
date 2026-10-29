using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Snare.App.Infrastructure;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Interop;

namespace Snare.App.Scrolling;

/// <summary>
/// A thin frame just outside the area being captured, so Matt can see what's in the shot. Its
/// inside is fully transparent (clicks and the wheel pass through), and it's excluded from capture.
/// </summary>
internal sealed class ScrollAreaBorder : Window
{
    private const int Thickness = 3;

    public ScrollAreaBorder(PixelRect area, MonitorInfo monitor)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        IsHitTestVisible = false;
        Content = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xE0, 0x1E, 0x88, 0xE5)),
            BorderThickness = new Thickness(Thickness / monitor.Scale),
        };

        SourceInitialized += (_, _) => CaptureExclusion.ExcludeFromCapture(new WindowInteropHelper(this).Handle);
        PhysicalPlacement.Pin(this, new PixelRect(area.X - Thickness, area.Y - Thickness, area.Width + (2 * Thickness), area.Height + (2 * Thickness)));
    }
}
