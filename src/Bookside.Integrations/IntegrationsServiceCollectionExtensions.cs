using Bookside.Core.Vocabulary.Lookup;
using Bookside.Integrations.Dictionary;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bookside.Integrations;

/// <summary>
/// Dictionary providers. Each <c>Add…Client()</c> adds one more <see cref="IDictionaryLookupService"/>; the
/// definition service tries them in registration order (see <c>IDefinitionService</c>). Adding a provider = one
/// client class + one method here that calls <see cref="AddDictionaryClient{TClient, TOptions}"/>.
/// </summary>
public static class IntegrationsServiceCollectionExtensions
{
    /// <summary>Largest dictionary response accepted; real ones are a few KB (large Wiktionary entries ~100 KB).</summary>
    public const long MaxDictionaryResponseBytes = 1024 * 1024;

    /// <summary>Registers dictionaryapi.dev as a dictionary provider.</summary>
    public static IHttpClientBuilder AddDictionaryApiClient(this IServiceCollection services, Action<DictionaryApiOptions>? configure = null) =>
        services.AddDictionaryClient<DictionaryApiClient, DictionaryApiOptions>(DictionaryApiClient.SourceId, o => o.BaseAddress, configure);

    /// <summary>Registers freedictionaryapi.com as a dictionary provider.</summary>
    public static IHttpClientBuilder AddFreeDictionaryApiClient(this IServiceCollection services, Action<FreeDictionaryApiOptions>? configure = null) =>
        services.AddDictionaryClient<FreeDictionaryApiClient, FreeDictionaryApiOptions>(FreeDictionaryApiClient.SourceId, o => o.BaseAddress, configure);

    /// <summary>
    /// Typed HttpClient + standard resilience. Adding a word looks its definition up inline, so the budget per
    /// provider is short: 3 s per attempt, 2 retries, 8 s total. Each provider gets its own client name (so its own
    /// base address and resilience options, <c>{name}-standard</c>). Returns the client builder so hosts and tests
    /// can adjust it (e.g. the primary handler).
    /// </summary>
    private static IHttpClientBuilder AddDictionaryClient<TClient, TOptions>(
        this IServiceCollection services,
        string name,
        Func<TOptions, Uri> baseAddress,
        Action<TOptions>? configure)
        where TClient : class, IDictionaryLookupService
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<TOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        var builder = services.AddHttpClient<IDictionaryLookupService, TClient>(name, (sp, client) =>
        {
            client.BaseAddress = baseAddress(sp.GetRequiredService<IOptions<TOptions>>().Value);
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
