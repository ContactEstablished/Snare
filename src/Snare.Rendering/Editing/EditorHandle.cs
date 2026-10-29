namespace Snare.Rendering.Editing;

/// <summary>A grab point on the selected object.</summary>
public enum EditorHandle
{
    None,
    Body,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,

    /// <summary>A line's or arrow's start point.</summary>
    Start,

    /// <summary>A line's or arrow's end point.</summary>
    End,

    /// <summary>A callout's tail point.</summary>
    Tail,
}
