namespace Snare.Capture.Scrolling;

/// <summary>What <see cref="ScrollStitcher.Add"/> did with a frame.</summary>
public enum StitchOutcome
{
    /// <summary>The first frame: the start of the image.</summary>
    Started,

    /// <summary>The frame moved down and its new rows were added.</summary>
    Appended,

    /// <summary>Nothing moved (or only something animated in place).</summary>
    NoMovement,

    /// <summary>The page was scrolled up; ignored until it comes back down.</summary>
    ScrolledBack,

    /// <summary>The frame couldn't be lined up with the previous one and was dropped.</summary>
    NoMatch,

    /// <summary>The image reached the height cap; no more frames are taken.</summary>
    LimitReached,
}
