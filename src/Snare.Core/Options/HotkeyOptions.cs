using Snare.Core.Hotkeys;

namespace Snare.Core.Options;

/// <summary>The gesture for each global hotkey, as text ("Ctrl+Shift+PrtSc"). Empty turns that hotkey off.</summary>
public sealed class HotkeyOptions
{
    public string? CaptureImage { get; set; } = "PrtSc";

    public string? RecordVideo { get; set; } = "Shift+PrtSc";

    public string? CaptureScrolling { get; set; } = "Ctrl+Shift+PrtSc";

    public string? RepeatLastRegion { get; set; } = "Ctrl+PrtSc";

    public string? GrabText { get; set; } = "Ctrl+Alt+PrtSc";

    public string? Get(HotkeyAction action) => action switch
    {
        HotkeyAction.CaptureImage => CaptureImage,
        HotkeyAction.RecordVideo => RecordVideo,
        HotkeyAction.CaptureScrolling => CaptureScrolling,
        HotkeyAction.RepeatLastRegion => RepeatLastRegion,
        HotkeyAction.GrabText => GrabText,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };

    public void Set(HotkeyAction action, string? gesture)
    {
        switch (action)
        {
            case HotkeyAction.CaptureImage:
                CaptureImage = gesture;
                break;
            case HotkeyAction.RecordVideo:
                RecordVideo = gesture;
                break;
            case HotkeyAction.CaptureScrolling:
                CaptureScrolling = gesture;
                break;
            case HotkeyAction.RepeatLastRegion:
                RepeatLastRegion = gesture;
                break;
            case HotkeyAction.GrabText:
                GrabText = gesture;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    /// <summary>The hotkeys that are on, parsed. Text that doesn't parse is left out (and reported by <see cref="Problems"/>).</summary>
    public IReadOnlyList<(HotkeyAction Action, HotkeyGesture Gesture)> Bindings() =>
        Enum.GetValues<HotkeyAction>()
            .Select(action => (action, text: Get(action)))
            .Where(b => !string.IsNullOrWhiteSpace(b.text))
            .Select(b => (b.action, ok: HotkeyGesture.TryParse(b.text, out var g), gesture: g))
            .Where(b => b.ok)
            .Select(b => (b.action, b.gesture!))
            .ToList();

    /// <summary>What's wrong with these settings: unreadable text, keys that fire while typing, and two actions on one key.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        var seen = new Dictionary<string, HotkeyAction>(StringComparer.Ordinal);
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            var text = Get(action);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (!HotkeyGesture.TryParse(text, out var gesture))
            {
                problems.Add($"\"{text}\" isn't a hotkey Snare understands.");
                continue;
            }

            if (gesture.Problem() is { } problem)
            {
                problems.Add(problem);
            }

            if (seen.TryGetValue(gesture.ToString(), out var other))
            {
                problems.Add($"{gesture} is set for both {Describe(other)} and {Describe(action)}.");
            }
            else
            {
                seen[gesture.ToString()] = action;
            }
        }

        return problems;
    }

    public static string Describe(HotkeyAction action) => action switch
    {
        HotkeyAction.CaptureImage => "Capture",
        HotkeyAction.RecordVideo => "Record video",
        HotkeyAction.CaptureScrolling => "Scrolling capture",
        HotkeyAction.RepeatLastRegion => "Repeat last region",
        HotkeyAction.GrabText => "Grab text",
        _ => action.ToString(),
    };
}
