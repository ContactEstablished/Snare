using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Snare.Interop;

public static class CaptureExclusion
{
    /// <summary>Keeps a window (such as the recording control bar) out of screenshots and recordings.</summary>
    public static bool ExcludeFromCapture(nint windowHandle) =>
        PInvoke.SetWindowDisplayAffinity((HWND)windowHandle, WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE);
}
