using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Snare.Interop;

/// <summary>
/// Scrolls whatever is under a point: through UI Automation's scroll pattern when the element (or
/// an ancestor) has one that scrolls vertically, otherwise with mouse-wheel input over the point.
/// Create and use it off the UI thread (UI Automation clients must not call into their own UI
/// thread's windows).
/// </summary>
public sealed class ScrollTarget
{
    private const int WheelDelta = 120;
    private const double PixelsPerNotch = 100; // Chrome and Edge, in DIPs
    private const int MaxAncestors = 25;
    private static readonly TimeSpan PatternSettle = TimeSpan.FromMilliseconds(300);

    private readonly int _x;
    private readonly int _y;
    private readonly double _viewDips;
    private IUIAutomationScrollPattern? _pattern;

    private ScrollTarget(int x, int y, double viewDips, IUIAutomationScrollPattern? pattern)
    {
        _x = x;
        _y = y;
        _viewDips = viewDips;
        _pattern = pattern;
    }

    /// <summary>"scroll pattern" or "mouse wheel", for the log.</summary>
    public string Method => _pattern is null ? "mouse wheel" : "scroll pattern";

    /// <summary>
    /// Finds the scrollable element under (x, y), physical pixels. <paramref name="areaHeight"/>
    /// (physical) and the monitor's <paramref name="scale"/> size a wheel step.
    /// </summary>
    public static ScrollTarget AtPoint(int x, int y, int areaHeight, double scale = 1) =>
        new(x, y, areaHeight / Math.Max(0.5, scale), FindScrollPattern(x, y));

    /// <summary>
    /// Scrolls by about <paramref name="viewFraction"/> of the view, down when positive. Through the
    /// scroll pattern when it works; the first time it doesn't move the page (checked for a moment,
    /// since browsers report the new position a little later), it switches to the wheel for good.
    /// </summary>
    public void Scroll(double viewFraction)
    {
        if (_pattern is { } pattern && TryPattern(pattern, viewFraction))
        {
            return;
        }

        _pattern = null;
        var notches = (int)Math.Round(_viewDips * Math.Abs(viewFraction) / PixelsPerNotch);
        Wheel(Math.Max(1, notches) * (viewFraction > 0 ? -1 : 1));
    }

    private static bool TryPattern(IUIAutomationScrollPattern pattern, double viewFraction)
    {
        try
        {
            var before = pattern.CurrentVerticalScrollPercent;
            var view = pattern.CurrentVerticalViewSize;
            if (before < 0 || view <= 0)
            {
                return false;
            }

            if ((viewFraction > 0 && before >= 100) || (viewFraction < 0 && before <= 0))
            {
                return true; // at the end already; the stitcher sees nothing new
            }

            pattern.SetScrollPercent(PInvoke.UIA_ScrollPatternNoScroll, Math.Clamp(before + (view * viewFraction), 0, 100));
            var clock = Stopwatch.StartNew();
            do
            {
                if (Math.Abs(pattern.CurrentVerticalScrollPercent - before) > 1e-6)
                {
                    return true;
                }

                Thread.Sleep(30);
            }
            while (clock.Elapsed < PatternSettle);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // COM, timeout or "element not available" errors: fall back to the wheel.
        }

        return false;
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // No UI Automation for this window (or it went away, or timed out): the wheel still works.
        }

        return null;
    }
}
