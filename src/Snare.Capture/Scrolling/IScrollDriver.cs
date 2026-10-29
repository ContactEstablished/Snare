namespace Snare.Capture.Scrolling;

/// <summary>Scrolls the captured area, for automatic mode.</summary>
public interface IScrollDriver
{
    /// <summary>
    /// Scrolls by about <paramref name="viewFraction"/> of the visible area: down when positive,
    /// up when negative. Returns false when it has no way to scroll.
    /// </summary>
    Task<bool> ScrollAsync(double viewFraction, CancellationToken cancellationToken);
}
