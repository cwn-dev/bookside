using Bookside.Core.Books.Services;
using Bookside.Core.Vocabulary.Services;
using Bookside.Data.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bookside.Data;

public static class BooksideServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF-backed application services. Requires a provider registration (e.g. AddBooksideSqlite)
    /// and an <see cref="Core.Vocabulary.Lookup.IDictionaryLookupService"/> (Bookside.Integrations).
    /// Scoped, because the dictionary client will be a typed HttpClient (transient); the services hold no context.
    /// </summary>
    public static IServiceCollection AddBooksideServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ILibraryService, LibraryService>();
        services.AddScoped<IVocabularyService, VocabularyService>();
        services.AddScoped<IDefinitionService, DefinitionService>();
        return services;
    }
}
