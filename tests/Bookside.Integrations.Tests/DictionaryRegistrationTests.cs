using System.Net;
using Bookside.Core.Vocabulary.Lookup;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.Integrations.Tests;

public sealed class DictionaryRegistrationTests
{
    [Fact]
    public async Task Providers_resolve_in_registration_order_each_with_its_own_base_address()
    {
        var free = new StubHttpMessageHandler().Reply(HttpStatusCode.OK, """{"word":"x","entries":[]}""");
        var dev = new StubHttpMessageHandler().Reply(HttpStatusCode.NotFound, """{"title":"No Definitions Found"}""");
        var services = new ServiceCollection();
        services.AddFreeDictionaryApiClient().ConfigurePrimaryHttpMessageHandler(() => free);
        services.AddDictionaryApiClient().ConfigurePrimaryHttpMessageHandler(() => dev);
        await using var provider = services.BuildServiceProvider();

        var lookups = provider.GetServices<IDictionaryLookupService>().ToList();
        foreach (var lookup in lookups)
        {
            await lookup.LookupAsync("x", cancellationToken: TestContext.Current.CancellationToken);
        }

        Assert.Equal(["freedictionaryapi.com", "dictionaryapi.dev"], lookups.Select(l => l.Source));
        Assert.Equal(new Uri("https://freedictionaryapi.com/api/v1/entries/en/x"), Assert.Single(free.Requests));
        Assert.Equal(new Uri("https://api.dictionaryapi.dev/api/v2/entries/en/x"), Assert.Single(dev.Requests));
    }
}
