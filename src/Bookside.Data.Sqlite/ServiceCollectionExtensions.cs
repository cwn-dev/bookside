using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.Data.Sqlite;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IDbContextFactory{BooksideDbContext}"/> (preferred in Blazor Server components, where a
    /// scope lives as long as the circuit) and a scoped <see cref="BooksideDbContext"/>.
    /// The host decides <paramref name="databasePath"/> (e.g. /data/bookside.db in the container).
    /// </summary>
    public static IServiceCollection AddBooksideSqlite(this IServiceCollection services, string databasePath)
    {
        ArgumentNullException.ThrowIfNull(services);

        var connectionString = BooksideSqlite.BuildConnectionString(databasePath);
        services.AddDbContextFactory<BooksideDbContext>(options => options.UseBooksideSqlite(connectionString));
        return services;
    }
}
