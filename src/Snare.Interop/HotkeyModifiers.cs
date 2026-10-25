namespace Snare.Interop;

/// <summary>Modifier flags for <see cref="GlobalHotkeys"/>; values match Win32 MOD_* constants.</summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,
    NoRepeat = 0x4000,
}
