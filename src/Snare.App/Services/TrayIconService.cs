using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Extensions.Logging;
using Snare.Core.Hotkeys;
using Snare.Core.Options;

namespace Snare.App.Services;

/// <summary>Owns the tray icon and its menu.</summary>
public sealed class TrayIconService(ILogger<TrayIconService> logger) : IDisposable
{
    private const string AppName = "Snare";

    // Created in Show, not here: WPF elements need the UI thread, and this service is built by DI.
    private readonly Dictionary<HotkeyAction, MenuItem> _hotkeyItems = [];
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
        menu.Items.Add(HotkeyItem(HotkeyAction.CaptureImage, "Capture", actions.Capture));
        var delayed = new MenuItem { Header = "Capture after delay" };
        foreach (var seconds in new[] { 3, 5, 10 })
        {
            delayed.Items.Add(Item($"{seconds} seconds", () => actions.CaptureAfterDelay(seconds)));
        }

        menu.Items.Add(delayed);
        menu.Items.Add(HotkeyItem(HotkeyAction.GrabText, "Grab text", actions.GrabText));
        menu.Items.Add(HotkeyItem(HotkeyAction.RecordVideo, "Record video", actions.RecordVideo));
        menu.Items.Add(HotkeyItem(HotkeyAction.CaptureScrolling, "Scrolling capture", actions.ScrollingCapture));
        menu.Items.Add(Item("Library", actions.OpenLibrary));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Settings…", actions.OpenSettings));
        menu.Items.Add(Item("Back up library", actions.BackUp));
        menu.Items.Add(new Separator());
        _spikesMenu = new MenuItem { Header = "Spikes" };
        menu.Items.Add(_spikesMenu);
        menu.Items.Add(new Separator());

        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => actions.Exit();
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

    /// <summary>Shows each action's current hotkey beside it in the menu (blank when it's off).</summary>
    public void UpdateHotkeyLabels(HotkeyOptions hotkeys)
    {
        ArgumentNullException.ThrowIfNull(hotkeys);
        foreach (var (action, item) in _hotkeyItems)
        {
            item.InputGestureText = HotkeyGesture.TryParse(hotkeys.Get(action), out var gesture) ? gesture.ToString() : "";
        }
    }

    /// <summary>Puts hotkey problems in the tooltip and shows them once as a notification; no problems clears the tooltip.</summary>
    public void ShowHotkeyConflicts(IReadOnlyList<HotkeyConflict> conflicts)
    {
        ArgumentNullException.ThrowIfNull(conflicts);
        if (_trayIcon is null)
        {
            return;
        }

        if (conflicts.Count == 0)
        {
            _trayIcon.ToolTipText = AppName;
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

    private MenuItem HotkeyItem(HotkeyAction action, string header, Action run)
    {
        var item = Item(header, run);
        _hotkeyItems[action] = item;
        return item;
    }

    private static MenuItem Item(string header, Action run, string? gesture = null)
    {
        var item = new MenuItem { Header = header, InputGestureText = gesture ?? "" };
        item.Click += (_, _) => run();
        return item;
    }
}
