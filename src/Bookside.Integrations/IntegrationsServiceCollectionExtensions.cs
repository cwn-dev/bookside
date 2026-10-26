using Bookside.Core.Books;
using Bookside.Core.Books.Lookup;
using Bookside.Core.Vocabulary.Lookup;
using Bookside.Integrations.Books;
using Bookside.Integrations.Dictionary;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bookside.Integrations;

/// <summary>
/// Dictionary and book metadata providers. Each <c>Add…Client()</c> adds one
/// more <see cref="IDictionaryLookupService"/> or
/// <see cref="IBookLookupService"/>; the definition / book metadata services
/// try them in registration order. Adding a provider = one client class + one
/// method here that calls
/// <see cref="AddLookupClient{TService, TClient, TOptions}"/>.
/// </summary>
public static class IntegrationsServiceCollectionExtensions
{
    /// <summary>
    /// Named HttpClient that downloads covers for every book provider (hosts
    /// and tests can adjust it).
    /// </summary>
    public const string BookCoverClientName = "book-covers";

    /// <summary>
    /// Largest lookup response accepted; real ones are a few KB (large
    /// Wiktionary entries ~100 KB).
    /// </summary>
    public const long MaxDictionaryResponseBytes = 1024 * 1024;

    /// <summary>
    /// Registers dictionaryapi.dev as a dictionary provider.
    /// </summary>
    public static IHttpClientBuilder AddDictionaryApiClient(
        this IServiceCollection services,
        Action<DictionaryApiOptions>? configure = null) =>
            services.AddLookupClient<
                IDictionaryLookupService,
                DictionaryApiClient,
                DictionaryApiOptions>(
                    DictionaryApiClient.SourceId,
                    o => o.BaseAddress,
                    MaxDictionaryResponseBytes,
                    configure);

    /// <summary>
    /// Registers freedictionaryapi.com as a dictionary provider.
    /// </summary>
    public static IHttpClientBuilder AddFreeDictionaryApiClient(
        this IServiceCollection services,
        Action<FreeDictionaryApiOptions>? configure = null) =>
            services.AddLookupClient<
                IDictionaryLookupService,
                FreeDictionaryApiClient,
                FreeDictionaryApiOptions>(
                    FreeDictionaryApiClient.SourceId,
                    o => o.BaseAddress,
                    MaxDictionaryResponseBytes,
                    configure);

    /// <summary>
    /// Registers Open Library as a book metadata provider.
    /// </summary>
    public static IHttpClientBuilder AddOpenLibraryClient(
        this IServiceCollection services,
        Action<OpenLibraryOptions>? configure = null) =>
            services
                .AddBookCoverDownloader()
                .AddLookupClient<IBookLookupService, OpenLibraryClient, OpenLibraryOptions>(
                    OpenLibraryClient.SourceId,
                    o => o.BaseAddress,
                    MaxDictionaryResponseBytes,
                    configure);

    /// <summary>
    /// Registers Google Books as a book metadata provider. Needs
    /// <see cref="GoogleBooksOptions.ApiKey"/> (keyless calls get 429).
    /// </summary>
    public static IHttpClientBuilder AddGoogleBooksClient(
        this IServiceCollection services,
        Action<GoogleBooksOptions>? configure = null) =>
            services
                .AddBookCoverDownloader()
                .AddLookupClient<IBookLookupService, GoogleBooksClient, GoogleBooksOptions>(
                    GoogleBooksClient.SourceId,
                    o => o.BaseAddress,
                    MaxDictionaryResponseBytes,
                    configure);

    /// <summary>
    /// Shared by the book providers; registered once. Covers are fetched on
    /// save, so the budget is longer than lookups': 15 s per attempt, 1 retry,
    /// 30 s total. Response cap = the largest cover accepted.
    /// </summary>
    private static IServiceCollection AddBookCoverDownloader(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(d => d.ServiceType == typeof(BookCoverDownloader)))
        {
            return services;
        }

        services
            .AddHttpClient<BookCoverDownloader>(BookCoverDownloader.ClientName, client =>
            {
                client.MaxResponseContentBufferSize = BookCover.MaxSizeBytes;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Bookside/1.0");
            })
            .AddStandardResilienceHandler(o =>
            {
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                o.Retry.MaxRetryAttempts = 1;
                o.Retry.Delay = TimeSpan.FromMilliseconds(500);
            });

        return services;
    }

    /// <summary>
    /// Typed HttpClient + standard resilience. Lookups run while the user waits
    /// (adding a word, the book form), so the budget per provider is short: 3 s
    /// per attempt, 2 retries, 8 s total. Each provider gets its own client
    /// name (so its own base address and resilience options,
    /// <c>{name}-standard</c>). Returns the client builder so hosts and tests
    /// can adjust it (e.g. the primary handler).
    /// </summary>
    private static IHttpClientBuilder AddLookupClient<TService, TClient, TOptions>(
        this IServiceCollection services,
        string name,
        Func<TOptions, Uri> baseAddress,
        long maxResponseBytes,
        Action<TOptions>? configure)
        where TService : class
        where TClient : class, TService
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<TOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        var builder = services.AddHttpClient<TService, TClient>(name, (sp, client) =>
        {
            var value = sp.GetRequiredService<IOptions<TOptions>>().Value;
            client.BaseAddress = baseAddress(value);
            client.MaxResponseContentBufferSize = maxResponseBytes;
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
