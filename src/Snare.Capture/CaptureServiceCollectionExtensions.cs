using Microsoft.Extensions.DependencyInjection;

namespace Snare.Capture;

public static class CaptureServiceCollectionExtensions
{
    /// <summary>Registers capture services. Phase 1 adds freeze-frame and window capture here.</summary>
    public static IServiceCollection AddSnareCapture(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
