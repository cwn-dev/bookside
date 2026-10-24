using Bookside.Core.Common;

namespace Bookside.Core.Vocabulary.Services;

/// <summary>
/// Cache-aware definitions: <see cref="DictionaryEntry"/> first, then each registered
/// <see cref="Lookup.IDictionaryLookupService"/> in order until one finds the term (a failure or NotFound moves on
/// to the next). Found is cached indefinitely, and so is NotFound once every provider says NotFound. If a provider
/// failed and none found the term, nothing is cached and the result is <see cref="ErrorType.Unavailable"/>.
/// </summary>
public interface IDefinitionService
{
    /// <param name="refresh">Skip the cache and re-run the whole chain; the cached entry is overwritten only if it succeeds.</param>
    Task<Result<DefinitionDto>> GetDefinitionAsync(
        string term,
        string language = "en",
        bool refresh = false,
        CancellationToken cancellationToken = default);
}
