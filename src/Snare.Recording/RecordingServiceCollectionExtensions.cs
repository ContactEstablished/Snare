using Microsoft.Extensions.DependencyInjection;

namespace Snare.Recording;

public static class RecordingServiceCollectionExtensions
{
    /// <summary>Registers recording services. Phase 4 adds the recorder and FFmpeg jobs here.</summary>
    public static IServiceCollection AddSnareRecording(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
