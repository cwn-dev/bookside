using Bookside.Core.Common;

namespace Bookside.Core.Vocabulary.Services;

/// <summary>
/// Cache-aware definitions: <see cref="DictionaryEntry"/> first, then <see cref="Lookup.IDictionaryLookupService"/>.
/// Found and NotFound are cached indefinitely; transient failures are not cached and return
/// <see cref="ErrorType.Unavailable"/>.
/// </summary>
public interface IDefinitionService
{
    /// <param name="refresh">Skip the cache and re-fetch; the cached entry is overwritten only if the fetch succeeds.</param>
    Task<Result<DefinitionDto>> GetDefinitionAsync(
        string term,
        string language = "en",
        bool refresh = false,
        CancellationToken cancellationToken = default);
}
