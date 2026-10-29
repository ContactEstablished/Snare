namespace Snare.App.Scrolling;

public enum ScrollPanelState
{
    /// <summary>The area is picked; waiting for Start or Auto-scroll.</summary>
    Ready,

    /// <summary>Grabbing while Matt scrolls.</summary>
    Manual,

    /// <summary>Snare scrolls the page itself.</summary>
    Automatic,

    /// <summary>Stopped; stitching and saving.</summary>
    Finishing,
}
