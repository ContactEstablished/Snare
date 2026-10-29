using Snare.Core.Imaging;

namespace Snare.Core.Captures;

/// <summary>One monitor's pixels at the moment of the freeze. Frame size equals the monitor bounds.</summary>
public sealed record FrozenMonitor(MonitorInfo Monitor, PixelBuffer Frame);
