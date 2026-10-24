using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bookside.Data.Sqlite;

/// <summary>
/// Used only by `dotnet ef` so migrations can be created without a host app:
///   dotnet ef migrations add &lt;Name&gt; --project src/Bookside.Data.Sqlite
/// Override the target file with BOOKSIDE_DB_PATH when running `dotnet ef database update`.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BooksideDbContext>
{
    public BooksideDbContext CreateDbContext(string[] args)
    {
        var path = Environment.GetEnvironmentVariable("BOOKSIDE_DB_PATH") ?? "bookside.design.db";
        var options = new DbContextOptionsBuilder<BooksideDbContext>();
        options.UseBooksideSqlite(BooksideSqlite.BuildConnectionString(path));

        return new BooksideDbContext(options.Options);
    }
}
