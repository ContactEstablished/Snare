using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Snare.Core.Hotkeys;

/// <summary>
/// A global hotkey such as Ctrl+Shift+PrtSc: modifiers plus one key. Keys are PrtSc, A–Z, 0–9
/// and F1–F24. Text form is "Ctrl+Alt+Shift+Win+Key", in that order.
/// </summary>
public sealed record HotkeyGesture(bool Control, bool Alt, bool Shift, bool Win, string Key)
{
    public const string PrintScreen = "PrtSc";

    public bool IsPlainPrintScreen => Key == PrintScreen && !Control && !Alt && !Shift && !Win;

    /// <summary>Whether <paramref name="key"/> is a key name a hotkey can use, in its canonical spelling.</summary>
    public static bool IsKnownKey(string key) => Canonical(key) == key;

    /// <summary>Parses "Ctrl+Shift+PrtSc" and the like, case-insensitively. Empty or unknown text fails.</summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out HotkeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        bool control = false, alt = false, shift = false, win = false;
        string? key = null;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries))
        {
            switch (raw.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL":
                    control = true;
                    break;
                case "ALT":
                    alt = true;
                    break;
                case "SHIFT":
                    shift = true;
                    break;
                case "WIN" or "WINDOWS":
                    win = true;
                    break;
                default:
                    if (key is not null || Canonical(raw) is not { } canonical)
                    {
                        return false;
                    }

                    key = canonical;
                    break;
            }
        }

        if (key is null)
        {
            return false;
        }

        gesture = new HotkeyGesture(control, alt, shift, win, key);
        return true;
    }

    /// <summary>
    /// Why this can't be a global hotkey, or null if it can: a letter or digit needs Ctrl or Alt,
    /// or typing it anywhere would take a capture.
    /// </summary>
    public string? Problem() =>
        Key.Length == 1 && !Control && !Alt
            ? $"{this} would fire while typing; add Ctrl or Alt."
            : null;

    public override string ToString()
    {
        var text = new StringBuilder();
        if (Control)
        {
            text.Append("Ctrl+");
        }

        if (Alt)
        {
            text.Append("Alt+");
        }

        if (Shift)
        {
            text.Append("Shift+");
        }

        if (Win)
        {
            text.Append("Win+");
        }

        return text.Append(Key).ToString();
    }

    private static string? Canonical(string key)
    {
        var upper = key.ToUpperInvariant();
        if (upper is "PRTSC" or "PRINTSCREEN" or "PRINT" or "PRTSCN" or "SNAPSHOT")
        {
            return PrintScreen;
        }

        if (upper.Length == 1 && (char.IsAsciiLetterUpper(upper[0]) || char.IsAsciiDigit(upper[0])))
        {
            return upper;
        }

        return upper.Length is 2 or 3 && upper[0] == 'F' && int.TryParse(upper.AsSpan(1), out var n) && n is >= 1 and <= 24 && upper[1] != '0'
            ? $"F{n}"
            : null;
    }
}
