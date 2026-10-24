using Bookside.Core.Vocabulary.Lookup;
using Bookside.Integrations.Dictionary;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bookside.Integrations;

public static class IntegrationsServiceCollectionExtensions
{
    /// <summary>Largest dictionary response accepted; real ones are a few KB.</summary>
    public const long MaxDictionaryResponseBytes = 1024 * 1024;

    /// <summary>
    /// Registers dictionaryapi.dev as the <see cref="IDictionaryLookupService"/> (typed HttpClient + standard resilience).
    /// Adding a word looks its definition up inline, so the time budget is short: 3 s per attempt, 2 retries, 8 s total.
    /// Returns the client builder so hosts and tests can adjust it (e.g. the primary handler).
    /// </summary>
    public static IHttpClientBuilder AddDictionaryApiClient(this IServiceCollection services, Action<DictionaryApiOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<DictionaryApiOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        var builder = services.AddHttpClient<IDictionaryLookupService, DictionaryApiClient>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IOptions<DictionaryApiOptions>>().Value.BaseAddress;
            client.MaxResponseContentBufferSize = MaxDictionaryResponseBytes;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Bookside/1.0");
        });

        builder.AddStandardResilienceHandler(o =>
        {
            o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
            o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
            o.Retry.MaxRetryAttempts = 2;
            o.Retry.Delay = TimeSpan.FromMilliseconds(300);
        });

        return builder;
    }
}
