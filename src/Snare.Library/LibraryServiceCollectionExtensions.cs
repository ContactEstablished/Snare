using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Snare.Core.Captures;
using Snare.Core.Options;
using Snare.Core.Search;

namespace Snare.Library;

public static class LibraryServiceCollectionExtensions
{
    /// <summary>Registers the library: paths and the SQLite capture store.</summary>
    public static IServiceCollection AddSnareLibrary(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(sp => LibraryPaths.FromOptions(sp.GetRequiredService<IOptions<SnareOptions>>().Value));
        services.AddSingleton<SqliteCaptureStore>();
        services.AddSingleton<ICaptureStore>(sp => sp.GetRequiredService<SqliteCaptureStore>());
        services.AddSingleton<ICaptureCatalog>(sp => sp.GetRequiredService<SqliteCaptureStore>());
        return services;
    }
}
