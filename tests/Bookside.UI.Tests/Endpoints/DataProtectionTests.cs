using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.UI.Tests.Endpoints;

public sealed class DataProtectionTests : IAsyncLifetime
{
    private readonly WebHostFactory _factory = new();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public void Keys_are_persisted_to_the_configured_directory()
    {
        var protector = _factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test");

        var protectedValue = protector.Protect("hello");

        Assert.Equal("hello", protector.Unprotect(protectedValue));
        Assert.NotEmpty(Directory.GetFiles(_factory.KeysPath, "key-*.xml"));
    }

    [Fact]
    public async Task Protected_values_survive_a_host_restart_with_the_same_keys()
    {
        var protectedValue = _factory.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("test").Protect("hello");

        // A second host over the same keys directory stands in for a restarted container.
        await using var restarted = _factory.WithWebHostBuilder(_ => { });
        var unprotected = restarted.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("test").Unprotect(protectedValue);

        Assert.Equal("hello", unprotected);
    }
}
