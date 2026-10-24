using Bookside.Data.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.Data.Tests;

/// <summary>A real, migrated SQLite file in a temp directory, wired through the production registration path.</summary>
public sealed class SqliteTestDatabase : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "bookside-tests", Guid.NewGuid().ToString("N"));
    private ServiceProvider? _services;

    public string DatabasePath => Path.Combine(_directory, "nested", "bookside.db");

    public IDbContextFactory<BooksideDbContext> Factory =>
        _services!.GetRequiredService<IDbContextFactory<BooksideDbContext>>();

    public BooksideDbContext CreateContext() => Factory.CreateDbContext();

    public async ValueTask InitializeAsync()
    {
        _services = new ServiceCollection().AddBooksideSqlite(DatabasePath).BuildServiceProvider();
        await _services.InitializeBooksideDatabaseAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }
}
