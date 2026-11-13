using Bookside.Core.Vocabulary.Lookup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bookside.Api.Tests;

/// <summary>
/// The real Bookside.Web host on its own temp SQLite file, with the API token
/// set (null = API off) and an offline dictionary that knows no words.
/// </summary>
public sealed class ApiHostFactory(string? token = ApiHostFactory.Token) : WebApplicationFactory<Program>
{
    public const string Token = "test-token";

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "bookside-api-tests",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder
            .UseEnvironment("Testing")
            .UseSetting("Bookside:DatabasePath", Path.Combine(_directory, "bookside.db"))
            .UseSetting("Bookside:DataProtectionKeysPath", Path.Combine(_directory, "keys"))
            .UseSetting("Bookside:ApiToken", token)
            .UseSetting("Logging:LogLevel:Default", "Warning")
            .ConfigureTestServices(services =>
            {
                services.RemoveAll<IDictionaryLookupService>();
                services.AddSingleton<IDictionaryLookupService, NotFoundDictionary>();
            });

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class NotFoundDictionary : IDictionaryLookupService
    {
        public string Source => "test";

        public Task<DictionaryLookupResult> LookupAsync(
            string term,
            string language = "en",
            CancellationToken cancellationToken = default) =>
                Task.FromResult(DictionaryLookupResult.NotFound());
    }
}
