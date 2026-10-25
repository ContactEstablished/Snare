using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using H.NotifyIcon;
using Microsoft.Extensions.Logging;

namespace Snare.App.Services;

/// <summary>Owns the tray icon and its menu. Capture and Library light up in Phase 1.</summary>
public sealed class TrayIconService(ILogger<TrayIconService> logger) : IDisposable
{
    private TaskbarIcon? _trayIcon;

    public void Show()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "Capture (Phase 1)", IsEnabled = false });
        menu.Items.Add(new MenuItem { Header = "Library (Phase 1)", IsEnabled = false });
        menu.Items.Add(new Separator());

        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(exit);

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Snare",
            ContextMenu = menu,
            IconSource = new GeneratedIconSource
            {
                Text = "S",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x6F, 0xEB)),
            },
        };
        _trayIcon.ForceCreate();

        logger.LogInformation("Tray icon created");
    }

    public void Dispose() => _trayIcon?.Dispose();
}
