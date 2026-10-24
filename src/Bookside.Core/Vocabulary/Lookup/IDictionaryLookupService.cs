namespace Bookside.Core.Vocabulary.Lookup;

/// <summary>
/// Raw external dictionary lookup (planned implementation: dictionaryapi.dev). Callers should go through a
/// cache-aware service that checks <see cref="DictionaryEntry"/> first; this interface always hits the provider.
/// </summary>
public interface IDictionaryLookupService
{
    /// <summary>Provider id stored in <see cref="DictionaryEntry.Source"/>, e.g. "dictionaryapi.dev".</summary>
    string Source { get; }

    /// <summary>
    /// <see cref="DictionaryLookupResult.Status"/> is Found or NotFound (both cacheable).
    /// Transient failures (network, 5xx, rate limiting) throw <see cref="DictionaryLookupException"/> and must not be cached.
    /// </summary>
    Task<DictionaryLookupResult> LookupAsync(string term, string language = "en", CancellationToken cancellationToken = default);
}
