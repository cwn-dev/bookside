using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bookside.Data.Sqlite;

/// <summary>
/// Single place that defines how Bookside talks to SQLite (used by DI and
/// design-time tooling).
/// </summary>
public static class BooksideSqlite
{
    public static string BuildConnectionString(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        return new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = true,
        }.ToString();
    }

    public static DbContextOptionsBuilder UseBooksideSqlite(this DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.UseSqlite(connectionString, sqlite =>
        {
            // Migrations live here, not next to the provider-agnostic DbContext.
            sqlite.MigrationsAssembly(typeof(BooksideSqlite).Assembly);
        });
    }
}
