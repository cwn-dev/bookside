using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.Data.Sqlite;

public static class DatabaseInitializer
{
    /// <summary>
    /// Call once at host startup: creates the directory, applies pending migrations and enables WAL.
    /// EF Core 9+ takes a migration lock and throws if the model has changes with no migration.
    /// </summary>
    public static async Task InitializeBooksideDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var factory = services.GetRequiredService<IDbContextFactory<BooksideDbContext>>();
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var dataSource = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        var directory = Path.GetDirectoryName(dataSource);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        // WAL is persistent in the file: concurrent reads during writes, fewer fsyncs.
        // Requires a local filesystem (Docker volume/bind mount is fine; NFS/SMB is not).
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);
    }
}
