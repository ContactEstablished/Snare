using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Snare.Core.Options;

namespace Snare.Library;

public static class LibraryServiceCollectionExtensions
{
    /// <summary>Registers library services. Phase 1 adds the database context and capture store here.</summary>
    public static IServiceCollection AddSnareLibrary(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(sp => LibraryPaths.FromOptions(sp.GetRequiredService<IOptions<SnareOptions>>().Value));
        return services;
    }
}
