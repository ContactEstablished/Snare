using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Snare.Core.Captures;

namespace Snare.Capture;

public static class CaptureServiceCollectionExtensions
{
    /// <summary>Registers capture services: freeze-frame now, window capture in Phase 1.</summary>
    public static IServiceCollection AddSnareCapture(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IScreenFreezer, GdiScreenFreezer>();
        return services;
    }
}
