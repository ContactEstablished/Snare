using Snare.Core.Captures;
using Snare.Core.Geometry;

namespace Snare.Capture.Selection;

/// <summary>
/// Everything the capture overlay does with the pointer and keyboard, in physical pixels and
/// with no UI types, so it can be tested. The overlay forwards input here and draws the state.
/// </summary>
public sealed class SelectionSession
{
    /// <summary>A press-and-release that moves less than this is a click, which takes a window.</summary>
    public const int ClickThreshold = 4;

    private readonly FrozenDesktop _desktop;
    private readonly int _handleTolerance;
    private PixelPoint _anchor;
    private PixelRect _dragStartSelection;
    private SelectionHandle _activeHandle;
    private PixelRect? _regionBeforePress;

    public SelectionSession(FrozenDesktop desktop, int handleTolerance = 8)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        _desktop = desktop;
        _handleTolerance = handleTolerance;
        Pointer = desktop.Cursor;
        HoveredWindow = desktop.WindowAt(Pointer);
    }

    public SelectionPhase Phase { get; private set; } = SelectionPhase.Hovering;

    /// <summary>The region being drawn or adjusted; null while hovering.</summary>
    public PixelRect? Selection { get; private set; }

    /// <summary>The window that a click would take, while hovering.</summary>
    public WindowInfo? HoveredWindow { get; private set; }

    public PixelPoint Pointer { get; private set; }

    public SelectionResult? Result { get; private set; }

    public bool IsFinished => Phase is SelectionPhase.Accepted or SelectionPhase.Cancelled;

    /// <summary>What the overlay should highlight: the region, or else the hovered window.</summary>
    public PixelRect? Highlight => Selection ?? HoveredWindow?.Bounds.Intersect(_desktop.Bounds);

    /// <summary>Raised after any change, so the overlay can redraw.</summary>
    public event EventHandler? Changed;

    public void PointerMove(PixelPoint point)
    {
        if (IsFinished)
        {
            return;
        }

        Pointer = Clamp(point);
        switch (Phase)
        {
            case SelectionPhase.Hovering:
                HoveredWindow = _desktop.WindowAt(Pointer);
                break;
            case SelectionPhase.Drawing:
                Selection = IsClick(_anchor, Pointer) ? _regionBeforePress : Inclusive(_anchor, Pointer);
                break;
            case SelectionPhase.Moving:
                Selection = SelectionNudge.Move(
                    _dragStartSelection, Pointer.X - _anchor.X, Pointer.Y - _anchor.Y, large: false, _desktop.Bounds);
                break;
            case SelectionPhase.Resizing:
                Selection = Resize(_dragStartSelection, _activeHandle, Pointer);
                break;
        }

        OnChanged();
    }

    public void PointerDown(PixelPoint point)
    {
        if (IsFinished)
        {
            return;
        }

        Pointer = Clamp(point);
        _anchor = Pointer;

        if (Phase == SelectionPhase.Adjusting && Selection is { } current)
        {
            var handle = HandleAt(Pointer);
            if (handle == SelectionHandle.Inside)
            {
                Phase = SelectionPhase.Moving;
                _dragStartSelection = current;
                OnChanged();
                return;
            }

            if (handle != SelectionHandle.None)
            {
                Phase = SelectionPhase.Resizing;
                _activeHandle = handle;
                _dragStartSelection = current;
                OnChanged();
                return;
            }
        }

        // Anywhere else starts a new region. If the pointer doesn't move, it is a click: from
        // hovering that takes a window; with a region already drawn it keeps that region.
        _regionBeforePress = Phase == SelectionPhase.Adjusting ? Selection : null;
        Phase = SelectionPhase.Drawing;
        Selection = _regionBeforePress;
        OnChanged();
    }

    public void PointerUp(PixelPoint point)
    {
        if (IsFinished)
        {
            return;
        }

        Pointer = Clamp(point);
        switch (Phase)
        {
            case SelectionPhase.Drawing when IsClick(_anchor, Pointer) && _regionBeforePress is { } kept:
                // A stray click beside a drawn region keeps it rather than capturing a window.
                Selection = kept;
                Phase = SelectionPhase.Adjusting;
                break;
            case SelectionPhase.Drawing when IsClick(_anchor, Pointer):
                TakeWindowAt(_anchor);
                return;
            case SelectionPhase.Drawing:
                Selection = Inclusive(_anchor, Pointer);
                Phase = SelectionPhase.Adjusting;
                break;
            case SelectionPhase.Moving or SelectionPhase.Resizing:
                Phase = SelectionPhase.Adjusting;
                break;
        }

        OnChanged();
    }

    /// <summary>Double-click inside the region accepts it.</summary>
    public void DoubleClick(PixelPoint point)
    {
        if (Selection is { } current && current.Contains(Clamp(point)))
        {
            Finish(new SelectionResult(current, CaptureTarget.Region));
        }
    }

    /// <summary>Enter: accepts the region, or the hovered window when there is no region.</summary>
    public void Accept()
    {
        if (Selection is { } current)
        {
            Finish(new SelectionResult(current, CaptureTarget.Region));
        }
        else if (Phase == SelectionPhase.Hovering)
        {
            TakeWindowAt(Pointer);
        }
    }

    public void Cancel()
    {
        if (IsFinished)
        {
            return;
        }

        Phase = SelectionPhase.Cancelled;
        Result = null;
        OnChanged();
    }

    /// <summary>Arrow keys: move the region 1 px, or 10 px with Shift.</summary>
    public void Nudge(int stepsX, int stepsY, bool large)
    {
        if (Phase != SelectionPhase.Adjusting || Selection is not { } current)
        {
            return;
        }

        Selection = SelectionNudge.Move(current, stepsX, stepsY, large, _desktop.Bounds);
        OnChanged();
    }

    /// <summary>F: the monitor under the pointer.</summary>
    public void TakeMonitorAtPointer()
    {
        if (_desktop.MonitorAt(Pointer) is { } monitor)
        {
            Finish(new SelectionResult(monitor.Monitor.Bounds, CaptureTarget.Monitor));
        }
    }

    /// <summary>A: every monitor.</summary>
    public void TakeAllMonitors() => Finish(new SelectionResult(_desktop.Bounds, CaptureTarget.AllMonitors));

    /// <summary>Which part of the current region a point is over, for cursors and drags.</summary>
    public SelectionHandle HandleAt(PixelPoint point) =>
        Selection is { } current ? HitTest(current, point, _handleTolerance) : SelectionHandle.None;

    public static SelectionHandle HitTest(PixelRect rect, PixelPoint p, int tolerance)
    {
        // On a small region, full-size handles would cover it and it could never be moved.
        tolerance = Math.Min(tolerance, Math.Max(1, Math.Min(rect.Width, rect.Height) / 4));

        var right = rect.Right - 1;
        var bottom = rect.Bottom - 1;
        var nearLeft = Math.Abs(p.X - rect.X) <= tolerance;
        var nearRight = Math.Abs(p.X - right) <= tolerance;
        var nearTop = Math.Abs(p.Y - rect.Y) <= tolerance;
        var nearBottom = Math.Abs(p.Y - bottom) <= tolerance;
        var withinX = p.X >= rect.X - tolerance && p.X <= right + tolerance;
        var withinY = p.Y >= rect.Y - tolerance && p.Y <= bottom + tolerance;

        if (!withinX || !withinY)
        {
            return SelectionHandle.None;
        }

        return (nearLeft, nearRight, nearTop, nearBottom) switch
        {
            (true, _, true, _) => SelectionHandle.TopLeft,
            (_, true, true, _) => SelectionHandle.TopRight,
            (true, _, _, true) => SelectionHandle.BottomLeft,
            (_, true, _, true) => SelectionHandle.BottomRight,
            (true, _, _, _) => SelectionHandle.Left,
            (_, true, _, _) => SelectionHandle.Right,
            (_, _, true, _) => SelectionHandle.Top,
            (_, _, _, true) => SelectionHandle.Bottom,
            _ => rect.Contains(p) ? SelectionHandle.Inside : SelectionHandle.None,
        };
    }

    private void TakeWindowAt(PixelPoint point)
    {
        var window = _desktop.WindowAt(point);
        var bounds = window?.Bounds.Intersect(_desktop.Bounds);
        if (window is null || bounds is not { IsEmpty: false } rect)
        {
            // Nothing to take: go back to hovering.
            Phase = SelectionPhase.Hovering;
            Selection = null;
            OnChanged();
            return;
        }

        Finish(new SelectionResult(rect, CaptureTarget.Window, window));
    }

    private void Finish(SelectionResult result)
    {
        if (IsFinished)
        {
            return;
        }

        Result = result;
        Selection = result.Rect;
        Phase = SelectionPhase.Accepted;
        OnChanged();
    }

    /// <summary>Moves the dragged edges to the pointer; dragging past the opposite edge flips the region.</summary>
    private PixelRect Resize(PixelRect start, SelectionHandle handle, PixelPoint p)
    {
        var left = start.X;
        var top = start.Y;
        var right = start.Right - 1;
        var bottom = start.Bottom - 1;

        if (handle is SelectionHandle.Left or SelectionHandle.TopLeft or SelectionHandle.BottomLeft)
        {
            left = p.X;
        }

        if (handle is SelectionHandle.Right or SelectionHandle.TopRight or SelectionHandle.BottomRight)
        {
            right = p.X;
        }

        if (handle is SelectionHandle.Top or SelectionHandle.TopLeft or SelectionHandle.TopRight)
        {
            top = p.Y;
        }

        if (handle is SelectionHandle.Bottom or SelectionHandle.BottomLeft or SelectionHandle.BottomRight)
        {
            bottom = p.Y;
        }

        return Inclusive(new PixelPoint(left, top), new PixelPoint(right, bottom));
    }

    /// <summary>A region covering both corner pixels.</summary>
    private static PixelRect Inclusive(PixelPoint a, PixelPoint b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1);

    private static bool IsClick(PixelPoint a, PixelPoint b) =>
        Math.Abs(a.X - b.X) < ClickThreshold && Math.Abs(a.Y - b.Y) < ClickThreshold;

    private PixelPoint Clamp(PixelPoint p) => new(
        Math.Clamp(p.X, _desktop.Bounds.X, _desktop.Bounds.Right - 1),
        Math.Clamp(p.Y, _desktop.Bounds.Y, _desktop.Bounds.Bottom - 1));

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
