using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Extensions.Logging;
using Snare.Core.Hotkeys;

namespace Snare.App.Services;

/// <summary>Owns the tray icon and its menu. Capture and Library light up in Phase 1.</summary>
public sealed class TrayIconService(ILogger<TrayIconService> logger) : IDisposable
{
    private const string AppName = "Snare";

    private readonly MenuItem _spikesMenu = new() { Header = "Spikes" };
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
        menu.Items.Add(_spikesMenu);
        menu.Items.Add(new Separator());

        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(exit);

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = AppName,
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

    /// <summary>Adds an entry to the Spikes submenu (Phase 0 harnesses).</summary>
    public void AddSpike(string header, Action run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => run();
        _spikesMenu.Items.Add(item);
    }

    /// <summary>Puts hotkey problems in the tooltip and shows them once as a notification.</summary>
    public void ShowHotkeyConflicts(IReadOnlyList<HotkeyConflict> conflicts)
    {
        ArgumentNullException.ThrowIfNull(conflicts);
        if (_trayIcon is null || conflicts.Count == 0)
        {
            return;
        }

        _trayIcon.ToolTipText = $"{AppName}\n{string.Join("\n", conflicts.Select(c => c.Summary))}";
        Notify("Snare hotkeys", string.Join(" ", conflicts.Select(c => c.Advice).Distinct()));
    }

    public void Notify(string title, string message)
    {
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.ShowNotification(title, message, NotificationIcon.Info);
    }

    public void Dispose() => _trayIcon?.Dispose();
}
