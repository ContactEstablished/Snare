using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Snare.Interop;

/// <summary>
/// Scrolls whatever is under a point one step down: through UI Automation's scroll pattern when
/// the element (or an ancestor) has one that scrolls vertically, otherwise with mouse-wheel input
/// over the point. Create and use it off the UI thread (UI Automation clients must not call into
/// their own UI thread's windows).
/// </summary>
public sealed class ScrollTarget
{
    private const int WheelDelta = 120;
    private const int PixelsPerNotch = 100; // Chrome and Edge at 100% scaling
    private const int MaxAncestors = 25;
    private const int PatternFailuresBeforeWheel = 2;

    private readonly int _x;
    private readonly int _y;
    private readonly int _wheelNotches;
    private IUIAutomationScrollPattern? _pattern;
    private int _patternFailures;

    private ScrollTarget(int x, int y, int areaHeight, IUIAutomationScrollPattern? pattern)
    {
        _x = x;
        _y = y;
        _pattern = pattern;
        _wheelNotches = Math.Max(1, (int)Math.Round(areaHeight * 0.4 / PixelsPerNotch));
    }

    /// <summary>"scroll pattern" or "mouse wheel", for the log.</summary>
    public string Method => _pattern is null ? "mouse wheel" : "scroll pattern";

    /// <summary>Finds the scrollable element under (x, y), physical pixels; <paramref name="areaHeight"/> sizes a wheel step.</summary>
    public static ScrollTarget AtPoint(int x, int y, int areaHeight) => new(x, y, areaHeight, FindScrollPattern(x, y));

    /// <summary>
    /// Scrolls one step: half a view through the scroll pattern, or about 40% of the area by wheel.
    /// Falls back to the wheel for good once the pattern twice fails to move the page.
    /// </summary>
    public void ScrollDown()
    {
        if (_pattern is { } pattern)
        {
            try
            {
                var before = pattern.CurrentVerticalScrollPercent;
                if (before >= 100)
                {
                    return; // at the end; the stitcher sees nothing new and stops
                }

                var step = Math.Max(1, pattern.CurrentVerticalViewSize / 2);
                pattern.SetScrollPercent(PInvoke.UIA_ScrollPatternNoScroll, Math.Min(100, before + step));
                if (pattern.CurrentVerticalScrollPercent > before)
                {
                    _patternFailures = 0;
                    return;
                }
            }
            catch (COMException)
            {
            }

            if (++_patternFailures < PatternFailuresBeforeWheel)
            {
                return;
            }

            _pattern = null;
        }

        Wheel(-_wheelNotches);
    }

    private void Wheel(int notches)
    {
        PInvoke.SetCursorPos(_x, _y);
        Span<INPUT> input =
        [
            new INPUT
            {
                type = INPUT_TYPE.INPUT_MOUSE,
                Anonymous = { mi = new MOUSEINPUT { dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_WHEEL, mouseData = unchecked((uint)(notches * WheelDelta)) } },
            },
        ];
        PInvoke.SendInput(input, Marshal.SizeOf<INPUT>());
    }

    private static IUIAutomationScrollPattern? FindScrollPattern(int x, int y)
    {
        try
        {
            var automation = (IUIAutomation)new CUIAutomation();
            var walker = automation.ControlViewWalker;
            var element = automation.ElementFromPoint(new System.Drawing.Point(x, y));
            for (var i = 0; element is not null && i < MaxAncestors; i++)
            {
                if (element.GetCurrentPattern(UIA_PATTERN_ID.UIA_ScrollPatternId) is IUIAutomationScrollPattern pattern
                    && pattern.CurrentVerticallyScrollable)
                {
                    return pattern;
                }

                element = walker.GetParentElement(element);
            }
        }
        catch (COMException)
        {
            // No UI Automation for this window (or it went away): the wheel still works.
        }

        return null;
    }
}
