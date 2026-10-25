using Microsoft.Extensions.DependencyInjection;

namespace Snare.Rendering;

public static class RenderingServiceCollectionExtensions
{
    public static IServiceCollection AddSnareRendering(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<AnnotationRenderer>();
        return services;
    }
}
