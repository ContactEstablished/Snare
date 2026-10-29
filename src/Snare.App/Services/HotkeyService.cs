using System.Diagnostics;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Snare.Core.Hotkeys;
using Snare.Core.Options;
using Snare.Interop;

namespace Snare.App.Services;

/// <summary>
/// Registers Snare's global hotkeys (from Settings) on a hidden message-only window and raises
/// <see cref="Pressed"/> when one fires. Failed registrations become <see cref="Conflicts"/>.
/// When the hotkey settings change, everything is registered again.
/// </summary>
public sealed class HotkeyService(IOptionsMonitor<SnareOptions> options, ILogger<HotkeyService> logger) : IDisposable
{
    private const int WmHotkey = 0x0312;
    private static readonly nint MessageOnlyParent = new(-3);

    private readonly Dictionary<int, (HotkeyAction Action, HotkeyGesture Gesture)> _registered = [];
    private HwndSource? _window;
    private IDisposable? _onChange;
    private string _current = "";
    private bool _suspended;

    public event EventHandler<HotkeyAction>? Pressed;

    /// <summary>Raised after registering again, with the new <see cref="Conflicts"/>.</summary>
    public event EventHandler? ConflictsChanged;

    public IReadOnlyList<HotkeyConflict> Conflicts { get; private set; } = [];

    /// <summary>Registers every hotkey and follows setting changes. Call on the UI thread.</summary>
    public void Start()
    {
        if (_window is not null)
        {
            return;
        }

        _window = new HwndSource(new HwndSourceParameters("Snare hotkeys")
        {
            ParentWindow = MessageOnlyParent,
            WindowStyle = 0,
        });
        _window.AddHook(WndProc);
        Register();
        _onChange = options.OnChange((_, _) => _window?.Dispatcher.BeginInvoke(Register));
    }

    /// <summary>Registers the hotkeys in the current settings, if they differ from what's registered.</summary>
    public void Register()
    {
        if (_window is null || _suspended)
        {
            return;
        }

        var bindings = options.CurrentValue.Hotkeys.Bindings();
        var key = string.Join(";", bindings.Select(b => $"{b.Action}={b.Gesture}"));
        if (key == _current)
        {
            return;
        }

        UnregisterAll();
        _current = key;
        var failed = new List<HotkeyGesture>();
        foreach (var (action, gesture) in bindings)
        {
            var id = (int)action + 1;
            if (GlobalHotkeys.VirtualKeyFor(gesture.Key) is { } virtualKey
                && GlobalHotkeys.TryRegister(_window.Handle, id, ModifiersFor(gesture) | HotkeyModifiers.NoRepeat, virtualKey))
            {
                _registered[id] = (action, gesture);
                logger.LogInformation("Registered hotkey {Gesture} for {Action}", gesture, action);
            }
            else
            {
                failed.Add(gesture);
            }
        }

        Conflicts = Diagnose(failed);
        foreach (var conflict in Conflicts)
        {
            logger.LogWarning("Hotkey {Gesture} not registered: {Summary}. {Advice}",
                conflict.Gesture, conflict.Summary, conflict.Advice);
        }

        ConflictsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Unregisters everything while Settings is open, so pressing a hotkey there records it instead of firing it.</summary>
    public void Suspend()
    {
        if (_window is not null)
        {
            UnregisterAll();
        }

        _suspended = true;
    }

    /// <summary>Registers the current settings again after <see cref="Suspend"/>.</summary>
    public void Resume()
    {
        _suspended = false;
        Register();
    }

    public void Dispose()
    {
        _onChange?.Dispose();
        if (_window is null)
        {
            return;
        }

        UnregisterAll();
        _window.RemoveHook(WndProc);
        _window.Dispose();
        _window = null;
    }

    private void UnregisterAll()
    {
        foreach (var id in _registered.Keys)
        {
            GlobalHotkeys.Unregister(_window!.Handle, id);
        }

        _registered.Clear();
        _current = "";
    }

    private static HotkeyModifiers ModifiersFor(HotkeyGesture gesture) =>
        (gesture.Control ? HotkeyModifiers.Control : 0)
        | (gesture.Alt ? HotkeyModifiers.Alt : 0)
        | (gesture.Shift ? HotkeyModifiers.Shift : 0)
        | (gesture.Win ? HotkeyModifiers.Windows : 0);

    private static List<HotkeyConflict> Diagnose(List<HotkeyGesture> failed)
    {
        if (failed.Count == 0)
        {
            return [];
        }

        var processNames = Process.GetProcesses().Select(p =>
        {
            using (p)
            {
                return p.ProcessName;
            }
        }).ToList();
        var snippingOwnsPrintScreen = SnippingToolSetting.IsPrintScreenBoundToSnipping();

        return failed
            .Select(g => HotkeyConflictDiagnosis.Diagnose(g.ToString(), g.IsPlainPrintScreen, processNames, snippingOwnsPrintScreen))
            .ToList();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmHotkey && _registered.TryGetValue((int)wParam, out var binding))
        {
            handled = true;
            logger.LogInformation("Hotkey {Gesture} pressed", binding.Gesture);
            Pressed?.Invoke(this, binding.Action);
        }

        return 0;
    }
}
