using Snare.Capture.Selection;
using Snare.Core.Geometry;

namespace Snare.Capture.Tests;

public class SelectionSessionTests
{
    private static SelectionSession Session(int cursorX = 50, int cursorY = 35) =>
        new(TestDesktops.Stacked(new PixelPoint(cursorX, cursorY)));

    private static void Drag(SelectionSession s, int x1, int y1, int x2, int y2)
    {
        s.PointerDown(new PixelPoint(x1, y1));
        s.PointerMove(new PixelPoint((x1 + x2) / 2, (y1 + y2) / 2));
        s.PointerMove(new PixelPoint(x2, y2));
        s.PointerUp(new PixelPoint(x2, y2));
    }

    [Fact]
    public void Hovering_HighlightsTheTopmostWindowUnderThePointer()
    {
        var s = Session();

        s.PointerMove(new PixelPoint(15, 15));
        Assert.Equal(TestDesktops.Editor, s.HoveredWindow);

        s.PointerMove(new PixelPoint(60, 35));
        Assert.Equal(TestDesktops.Browser, s.HoveredWindow);
        Assert.Equal(TestDesktops.Browser.Bounds, s.Highlight);
    }

    [Fact]
    public void StartsHoveringTheWindowUnderTheFreezeCursor()
    {
        Assert.Equal(TestDesktops.Editor, Session(15, 15).HoveredWindow);
    }

    [Fact]
    public void Click_TakesTheHoveredWindow()
    {
        var s = Session();

        s.PointerDown(new PixelPoint(15, 15));
        s.PointerMove(new PixelPoint(17, 16)); // jitter under the click threshold
        s.PointerUp(new PixelPoint(17, 16));

        Assert.Equal(SelectionPhase.Accepted, s.Phase);
        Assert.Equal(new SelectionResult(TestDesktops.Editor.Bounds, CaptureTarget.Window, TestDesktops.Editor), s.Result);
    }

    [Fact]
    public void Click_ClipsAWindowHangingOffTheDesktop()
    {
        var s = Session();

        s.PointerDown(new PixelPoint(95, 35));
        s.PointerUp(new PixelPoint(95, 35));

        // OffScreenEdge is (90, 30, 40, 20); the primary ends at x 100, y 40.
        Assert.Equal(new PixelRect(90, 30, 10, 10), s.Result!.Rect);
    }

    [Fact]
    public void Drag_DrawsAnInclusiveRegionInAnyDirection()
    {
        var s = Session();

        Drag(s, 30, 20, 10, 5);

        Assert.Equal(SelectionPhase.Adjusting, s.Phase);
        Assert.Equal(new PixelRect(10, 5, 21, 16), s.Selection);
        Assert.Null(s.Result);
    }

    [Fact]
    public void Drag_CanCrossIntoTheUpperMonitor()
    {
        var s = Session();

        Drag(s, 30, 10, 50, -10);

        Assert.Equal(new PixelRect(30, -10, 21, 21), s.Selection);
    }

    [Fact]
    public void Drag_IsClampedToTheDesktop()
    {
        var s = Session();

        Drag(s, 90, 30, 500, 500);

        Assert.Equal(new PixelRect(90, 30, 10, 10), s.Selection);
    }

    [Fact]
    public void Enter_AcceptsTheRegion()
    {
        var s = Session();
        Drag(s, 10, 10, 20, 20);

        s.Accept();

        Assert.Equal(new SelectionResult(new PixelRect(10, 10, 11, 11), CaptureTarget.Region), s.Result);
        Assert.True(s.IsFinished);
    }

    [Fact]
    public void Enter_WhileHovering_TakesTheWindowUnderThePointer()
    {
        var s = Session();
        s.PointerMove(new PixelPoint(15, 15));

        s.Accept();

        Assert.Equal(CaptureTarget.Window, s.Result!.Target);
        Assert.Equal(TestDesktops.Editor, s.Result.Window);
    }

    [Fact]
    public void DoubleClickInside_Accepts_ButOutsideDoesNot()
    {
        var s = Session();
        Drag(s, 10, 10, 20, 20);

        s.DoubleClick(new PixelPoint(50, 30));
        Assert.False(s.IsFinished);

        s.DoubleClick(new PixelPoint(15, 15));
        Assert.Equal(CaptureTarget.Region, s.Result!.Target);
    }

    [Fact]
    public void DraggingInside_MovesTheRegion()
    {
        var s = Session();
        Drag(s, 10, 10, 20, 20);

        Drag(s, 15, 15, 25, 18);

        Assert.Equal(new PixelRect(20, 13, 11, 11), s.Selection);
        Assert.Equal(SelectionPhase.Adjusting, s.Phase);
    }

    [Fact]
    public void DraggingACorner_Resizes_AndFlipsPastTheOppositeEdge()
    {
        var s = Session();
        Drag(s, 10, 10, 20, 20);

        Drag(s, 20, 20, 30, 25); // bottom-right handle
        Assert.Equal(new PixelRect(10, 10, 21, 16), s.Selection);

        Drag(s, 10, 10, 40, 30); // top-left dragged past bottom-right
        Assert.Equal(new PixelRect(30, 25, 11, 6), s.Selection);
    }

    [Fact]
    public void DraggingAnEdge_MovesOnlyThatEdge()
    {
        var s = Session();
        Drag(s, 10, 10, 30, 30);

        Drag(s, 20, 10, 20, 2); // top edge

        Assert.Equal(new PixelRect(10, 2, 21, 29), s.Selection);
    }

    [Fact]
    public void ClickOutsideARegion_KeepsTheRegion_AndDoesNotTakeAWindow()
    {
        var s = Session();
        Drag(s, 10, 10, 20, 20);

        s.PointerDown(new PixelPoint(60, 35));
        s.PointerUp(new PixelPoint(61, 35));

        Assert.Equal(SelectionPhase.Adjusting, s.Phase);
        Assert.Equal(new PixelRect(10, 10, 11, 11), s.Selection);
        Assert.Null(s.Result);
    }

    [Fact]
    public void DragOutsideARegion_ReplacesIt()
    {
        var s = Session();
        Drag(s, 10, 10, 20, 20);

        Drag(s, 50, 25, 70, 35);

        Assert.Equal(new PixelRect(50, 25, 21, 11), s.Selection);
    }

    [Fact]
    public void ClickWithNoWindowUnderneath_KeepsHovering()
    {
        // The upper monitor has no windows in the test desktop.
        var s = Session();

        s.PointerDown(new PixelPoint(40, -20));
        s.PointerUp(new PixelPoint(40, -20));

        Assert.Equal(SelectionPhase.Hovering, s.Phase);
        Assert.Null(s.Result);
    }

    [Fact]
    public void F_OverTheGapBetweenMonitors_DoesNothing()
    {
        var s = Session();
        s.PointerMove(new PixelPoint(5, -20)); // left of the upper monitor, inside the bounding box

        s.TakeMonitorAtPointer();

        Assert.False(s.IsFinished);
    }

    [Fact]
    public void Moving_StopsAtTheDesktopEdge()
    {
        var s = Session();
        Drag(s, 10, 10, 20, 20);

        Drag(s, 15, 15, 300, 300);

        Assert.Equal(new PixelRect(89, 29, 11, 11), s.Selection);
    }

    [Fact]
    public void HitTest_ShrinksHandlesOnSmallRegions()
    {
        // 8x8 region: tolerance drops to 2, so the middle is Inside rather than a handle.
        Assert.Equal(SelectionHandle.Inside, SelectionSession.HitTest(new PixelRect(0, 0, 8, 8), new PixelPoint(4, 4), tolerance: 8));
        Assert.Equal(SelectionHandle.TopLeft, SelectionSession.HitTest(new PixelRect(0, 0, 8, 8), new PixelPoint(1, 1), tolerance: 8));
    }

    [Fact]
    public void Nudge_MovesTheRegionAndStaysOnTheDesktop()
    {
        var s = Session();
        Drag(s, 10, 10, 20, 20);

        s.Nudge(1, 0, large: false);
        Assert.Equal(new PixelRect(11, 10, 11, 11), s.Selection);

        s.Nudge(0, 1, large: true);
        s.Nudge(0, 1, large: true);
        Assert.Equal(new PixelRect(11, 29, 11, 11), s.Selection); // bottom of the desktop is y 40
    }

    [Fact]
    public void F_TakesTheMonitorUnderThePointer()
    {
        var s = Session();
        s.PointerMove(new PixelPoint(40, -20));

        s.TakeMonitorAtPointer();

        Assert.Equal(new SelectionResult(new PixelRect(20, -40, 60, 40), CaptureTarget.Monitor), s.Result);
    }

    [Fact]
    public void A_TakesTheWholeDesktop()
    {
        var s = Session();

        s.TakeAllMonitors();

        Assert.Equal(new PixelRect(0, -40, 100, 80), s.Result!.Rect);
        Assert.Equal(CaptureTarget.AllMonitors, s.Result.Target);
    }

    [Fact]
    public void Escape_CancelsAndIgnoresLaterInput()
    {
        var s = Session();
        Drag(s, 10, 10, 20, 20);

        s.Cancel();
        s.Accept();

        Assert.Equal(SelectionPhase.Cancelled, s.Phase);
        Assert.Null(s.Result);
    }

    [Theory]
    [InlineData(10, 10, SelectionHandle.TopLeft)]
    [InlineData(29, 10, SelectionHandle.TopRight)]
    [InlineData(10, 29, SelectionHandle.BottomLeft)]
    [InlineData(29, 29, SelectionHandle.BottomRight)]
    [InlineData(20, 10, SelectionHandle.Top)]
    [InlineData(20, 29, SelectionHandle.Bottom)]
    [InlineData(10, 20, SelectionHandle.Left)]
    [InlineData(29, 20, SelectionHandle.Right)]
    [InlineData(20, 20, SelectionHandle.Inside)]
    [InlineData(50, 50, SelectionHandle.None)]
    public void HitTest_FindsHandles(int x, int y, SelectionHandle expected)
    {
        Assert.Equal(expected, SelectionSession.HitTest(new PixelRect(10, 10, 20, 20), new PixelPoint(x, y), tolerance: 3));
    }

    [Fact]
    public void Changed_FiresOnInput()
    {
        var s = Session();
        var count = 0;
        s.Changed += (_, _) => count++;

        s.PointerMove(new PixelPoint(1, 1));
        s.Cancel();

        Assert.Equal(2, count);
    }
}
