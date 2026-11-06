using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bookside.UI;

public static class BooksideUiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the UI's own per-circuit state (e.g.
    /// <see cref="LibraryListState"/>) and the build's <see cref="AppInfo"/>.
    /// </summary>
    public static IServiceCollection AddBooksideUi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<LibraryListState>();
        services.TryAddSingleton(AppInfo.FromAssembly(typeof(AppInfo).Assembly));
        return services;
    }
}
