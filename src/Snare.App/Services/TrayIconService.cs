using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Extensions.Logging;
using Snare.Core.Hotkeys;

namespace Snare.App.Services;

/// <summary>Owns the tray icon and its menu.</summary>
public sealed class TrayIconService(ILogger<TrayIconService> logger) : IDisposable
{
    private const string AppName = "Snare";

    // Created in Show, not here: WPF elements need the UI thread, and this service is built by DI.
    private MenuItem? _spikesMenu;
    private TaskbarIcon? _trayIcon;

    public void Show(TrayMenuActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        if (_trayIcon is not null)
        {
            return;
        }

        var menu = new ContextMenu();
        menu.Items.Add(Item("Capture", actions.Capture, "PrtSc"));
        var delayed = new MenuItem { Header = "Capture after delay" };
        foreach (var seconds in new[] { 3, 5, 10 })
        {
            delayed.Items.Add(Item($"{seconds} seconds", () => actions.CaptureAfterDelay(seconds)));
        }

        menu.Items.Add(delayed);
        menu.Items.Add(Item("Grab text", actions.GrabText, "Ctrl+Alt+PrtSc"));
        menu.Items.Add(Item("Record video", actions.RecordVideo, "Shift+PrtSc"));
        menu.Items.Add(Item("Scrolling capture", actions.ScrollingCapture, "Ctrl+Shift+PrtSc"));
        menu.Items.Add(Item("Library", actions.OpenLibrary));
        menu.Items.Add(new Separator());
        _spikesMenu = new MenuItem { Header = "Spikes" };
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
        _spikesMenu?.Items.Add(item);
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

    private static MenuItem Item(string header, Action run, string? gesture = null)
    {
        var item = new MenuItem { Header = header, InputGestureText = gesture ?? "" };
        item.Click += (_, _) => run();
        return item;
    }
}
