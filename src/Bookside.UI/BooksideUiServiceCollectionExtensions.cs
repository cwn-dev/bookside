using Microsoft.Extensions.DependencyInjection;

namespace Bookside.UI;

public static class BooksideUiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the UI's own per-circuit state (e.g.
    /// <see cref="LibraryListState"/>).
    /// </summary>
    public static IServiceCollection AddBooksideUi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<LibraryListState>();
        return services;
    }
}
