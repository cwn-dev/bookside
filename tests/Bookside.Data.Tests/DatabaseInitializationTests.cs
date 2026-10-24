using Microsoft.EntityFrameworkCore;

namespace Bookside.Data.Tests;

public sealed class DatabaseInitializationTests(SqliteTestDatabase database) : IClassFixture<SqliteTestDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Creates_database_file_and_missing_directories() =>
        Assert.True(File.Exists(database.DatabasePath));

    [Fact]
    public async Task Applies_all_migrations()
    {
        await using var db = database.CreateContext();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Ct));
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(Ct), m => m.EndsWith("_InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Model_matches_latest_migration()
    {
        await using var db = database.CreateContext();
        Assert.False(db.Database.HasPendingModelChanges(), "Model changed without a migration. Run: dotnet ef migrations add <Name> --project src/Bookside.Data.Sqlite");
    }

    [Fact]
    public async Task Enables_wal_and_foreign_keys()
    {
        await using var db = database.CreateContext();
        Assert.Equal("wal", (await db.Database.SqlQueryRaw<string>("PRAGMA journal_mode").ToListAsync(Ct)).Single());
        Assert.Equal(1, (await db.Database.SqlQueryRaw<int>("PRAGMA foreign_keys").ToListAsync(Ct)).Single());
    }
}
