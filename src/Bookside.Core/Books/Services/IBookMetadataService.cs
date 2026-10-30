using Bookside.Core.Books.Lookup;
using Bookside.Core.Common;

namespace Bookside.Core.Books.Services;

/// <summary>
/// Book metadata import: searches every registered
/// <see cref="IBookLookupService"/> at once and merges their results, taking
/// them in turns in registration order (each result keeps its
/// <see cref="BookMetadata.Source"/>). A failing provider is skipped; if one
/// failed and none had results, the result is
/// <see cref="ErrorType.Unavailable"/>.
/// </summary>
public interface IBookMetadataService
{
    /// <summary>
    /// A valid ISBN-10/13 (hyphens allowed) is looked up as an ISBN; anything
    /// else is a text search. Values longer than the book's limits and invalid
    /// ISBNs are dropped from the results. Each provider returns at most 10.
    /// </summary>
    Task<Result<IReadOnlyList<BookMetadata>>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fills in what the search left out (the description with its
    /// paragraphs) through the provider that returned <paramref name="book"/>.
    /// A description that is too long keeps the one from the search.
    /// </summary>
    Task<Result<BookMetadata>> GetDetailsAsync(
        BookMetadata book,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads <see cref="BookMetadata.CoverImageUrl"/> through the provider
    /// that returned it.
    /// </summary>
    Task<Result<CoverInput>> DownloadCoverAsync(
        BookMetadata book,
        CancellationToken cancellationToken = default);
}
