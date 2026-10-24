using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Bookside.UI.Tests.Endpoints;

/// <summary>The real Bookside.Web host (Program.cs) on its own temp SQLite file.</summary>
public sealed class WebHostFactory : WebApplicationFactory<Program>
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "bookside-web-tests", Guid.NewGuid().ToString("N"));

    public string DatabasePath => Path.Combine(_directory, "bookside.db");

    public string KeysPath => Path.Combine(_directory, "keys");

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder
            .UseEnvironment("Testing")
            .UseSetting("Bookside:DatabasePath", DatabasePath)
            .UseSetting("Bookside:DataProtectionKeysPath", KeysPath)
            .UseSetting("Logging:LogLevel:Default", "Warning");

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
