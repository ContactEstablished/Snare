using System.Diagnostics;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using Snare.Core.Hotkeys;
using Snare.Interop;

namespace Snare.App.Services;

/// <summary>
/// Registers Snare's global hotkeys on a hidden message-only window and raises
/// <see cref="Pressed"/> when one fires. Failed registrations become <see cref="Conflicts"/>.
/// </summary>
public sealed class HotkeyService(ILogger<HotkeyService> logger) : IDisposable
{
    private const int WmHotkey = 0x0312;
    private static readonly nint MessageOnlyParent = new(-3);

    private static readonly HotkeyBinding[] Bindings =
    [
        new(1, HotkeyAction.CaptureImage, HotkeyModifiers.None, "PrtSc"),
        new(2, HotkeyAction.RecordVideo, HotkeyModifiers.Shift, "Shift+PrtSc"),
        new(3, HotkeyAction.CaptureScrolling, HotkeyModifiers.Control | HotkeyModifiers.Shift, "Ctrl+Shift+PrtSc"),
        new(4, HotkeyAction.RepeatLastRegion, HotkeyModifiers.Control, "Ctrl+PrtSc"),
        new(5, HotkeyAction.GrabText, HotkeyModifiers.Control | HotkeyModifiers.Alt, "Ctrl+Alt+PrtSc"),
    ];

    private readonly List<HotkeyBinding> _registered = [];
    private HwndSource? _window;

    public event EventHandler<HotkeyAction>? Pressed;

    public IReadOnlyList<HotkeyConflict> Conflicts { get; private set; } = [];

    /// <summary>Registers every hotkey. Call on the UI thread.</summary>
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

        var failed = new List<HotkeyBinding>();
        foreach (var binding in Bindings)
        {
            var modifiers = binding.Modifiers | HotkeyModifiers.NoRepeat;
            if (GlobalHotkeys.TryRegister(_window.Handle, binding.Id, modifiers, GlobalHotkeys.PrintScreenKey))
            {
                _registered.Add(binding);
                logger.LogInformation("Registered hotkey {Gesture} for {Action}", binding.Gesture, binding.Action);
            }
            else
            {
                failed.Add(binding);
            }
        }

        Conflicts = Diagnose(failed);
        foreach (var conflict in Conflicts)
        {
            logger.LogWarning("Hotkey {Gesture} not registered: {Summary}. {Advice}",
                conflict.Gesture, conflict.Summary, conflict.Advice);
        }
    }

    public void Dispose()
    {
        if (_window is null)
        {
            return;
        }

        foreach (var binding in _registered)
        {
            GlobalHotkeys.Unregister(_window.Handle, binding.Id);
        }

        _registered.Clear();
        _window.RemoveHook(WndProc);
        _window.Dispose();
        _window = null;
    }

    private static List<HotkeyConflict> Diagnose(List<HotkeyBinding> failed)
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
            .Select(b => HotkeyConflictDiagnosis.Diagnose(
                b.Gesture, b.Modifiers == HotkeyModifiers.None, processNames, snippingOwnsPrintScreen))
            .ToList();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmHotkey && Bindings.FirstOrDefault(b => b.Id == (int)wParam) is { } binding)
        {
            handled = true;
            logger.LogInformation("Hotkey {Gesture} pressed", binding.Gesture);
            Pressed?.Invoke(this, binding.Action);
        }

        return 0;
    }

    private sealed record HotkeyBinding(int Id, HotkeyAction Action, HotkeyModifiers Modifiers, string Gesture);
}
