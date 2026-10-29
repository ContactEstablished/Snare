using Snare.Core.Geometry;
using Snare.Interop;

namespace Snare.Capture.Scrolling;

/// <summary>Automatic mode on the real screen: scrolls what's under the area's center (scroll pattern, else the wheel).</summary>
public sealed class ScreenScrollDriver : IScrollDriver
{
    private readonly ScrollTarget _target;

    private ScreenScrollDriver(ScrollTarget target) => _target = target;

    /// <summary>"scroll pattern" or "mouse wheel".</summary>
    public string Method => _target.Method;

    /// <summary>Finds the scrollable element under the area's center. Call off the UI thread.</summary>
    public static ScreenScrollDriver For(PixelRect area) =>
        new(ScrollTarget.AtPoint(area.X + (area.Width / 2), area.Y + (area.Height / 2), area.Height));

    public Task<bool> ScrollAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _target.ScrollDown();
        return Task.FromResult(true);
    }
}
