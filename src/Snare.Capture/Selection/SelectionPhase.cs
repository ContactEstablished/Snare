namespace Snare.Capture.Selection;

public enum SelectionPhase
{
    /// <summary>No region yet; the window under the pointer is highlighted.</summary>
    Hovering,

    /// <summary>The button is down and a new region is being drawn (or it may still be a click).</summary>
    Drawing,

    /// <summary>A region exists with handles; it can be moved, resized, nudged or accepted.</summary>
    Adjusting,

    Moving,

    Resizing,

    Accepted,

    Cancelled,
}
