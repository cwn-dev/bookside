using Bookside.Core.Common;

namespace Bookside.Core.Vocabulary.Services;

/// <summary>My vocabulary list. Words are unique per (language, normalized term).</summary>
public interface IVocabularyService
{
    Task<PagedResult<WordSummary>> ListWordsAsync(WordQuery query, CancellationToken cancellationToken = default);

    Task<Result<WordDetails>> GetWordAsync(Guid wordId, CancellationToken cancellationToken = default);

    /// <summary>The word with this term after normalization, or null. Used to point at an existing word on a duplicate.</summary>
    Task<WordSummary?> FindWordAsync(string term, string language = "en", CancellationToken cancellationToken = default);

    /// <summary>
    /// A random word that has a definition, other than <paramref name="excludeWordId"/> when another one exists.
    /// Null when no word has a definition.
    /// </summary>
    Task<WordSummary?> GetRandomWordAsync(Guid? excludeWordId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the word, then tries to attach a definition (cache, then dictionary API). If the dictionary is
    /// unavailable the word is still saved with no definition; use <see cref="DefineWordAsync"/> to retry.
    /// </summary>
    Task<Result<WordDetails>> AddWordAsync(WordInput input, CancellationToken cancellationToken = default);

    /// <summary>Replaces all editable fields. Changing the term re-links the definition.</summary>
    Task<Result<WordDetails>> UpdateWordAsync(Guid wordId, WordInput input, CancellationToken cancellationToken = default);

    Task<Result> DeleteWordAsync(Guid wordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches a definition. <paramref name="refresh"/> bypasses the cache and re-fetches from the API (the only
    /// way to re-check a cached NotFound, which never expires).
    /// </summary>
    Task<Result<WordDetails>> DefineWordAsync(Guid wordId, bool refresh = false, CancellationToken cancellationToken = default);
}
