namespace Bookside.Core.Search.Services;

/// <summary>
/// The global search: books, words, tags and comments in one call (one round
/// trip for a future API client).
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Matches <paramref name="query"/> anywhere in book titles (and subtitles,
    /// series and authors), word terms, tag names and comment text. A query
    /// shorter than <see cref="SearchResults.MinQueryLength"/> returns empty
    /// sections.
    /// </summary>
    Task<SearchResults> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}
