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

    /// <summary>Extra registrations (e.g. application services) on top of the SQLite provider.</summary>
    public Action<IServiceCollection>? ConfigureServices { get; init; }

    public IServiceProvider Services => _services!;

    public IDbContextFactory<BooksideDbContext> Factory =>
        _services!.GetRequiredService<IDbContextFactory<BooksideDbContext>>();

    public BooksideDbContext CreateContext() => Factory.CreateDbContext();

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection().AddBooksideSqlite(DatabasePath);
        ConfigureServices?.Invoke(services);
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
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
