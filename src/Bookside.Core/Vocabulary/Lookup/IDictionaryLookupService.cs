namespace Bookside.Core.Vocabulary.Lookup;

/// <summary>
/// One external dictionary provider (implementations in Bookside.Integrations). 
/// Several can be registered; the cache-aware <see cref="Services.IDefinitionService"/>
/// tries them in registration order. This interface always hits the provider.
/// </summary>
public interface IDictionaryLookupService
{
    /// <summary>
    /// Provider id stored in <see cref="DictionaryEntry.Source"/>,
    /// e.g. "dictionaryapi.dev".
    /// </summary>
    string Source { get; }

    /// <summary>
    /// <see cref="DictionaryLookupResult.Status"/> is Found or NotFound
    /// (both cacheable).
    /// Transient failures (network, 5xx, rate limiting) throw
    /// <see cref="DictionaryLookupException"/> and must not be cached.
    /// </summary>
    Task<DictionaryLookupResult> LookupAsync(
        string term,
        string language = "en",
        CancellationToken cancellationToken = default);
}
