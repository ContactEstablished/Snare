namespace Snare.Capture.Scrolling;

/// <summary>Scrolls the captured area one step down, for automatic mode.</summary>
public interface IScrollDriver
{
    /// <summary>Scrolls one step. Returns false when it has no way to scroll.</summary>
    Task<bool> ScrollAsync(CancellationToken cancellationToken);
}
