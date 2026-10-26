using Bookside.Core.Books.Lookup;
using Bookside.Core.Common;

namespace Bookside.Core.Books.Services;

/// <summary>
/// Book metadata import: tries each registered <see cref="IBookLookupService"/>
/// in order; the first provider with results wins (no merging). A failure or no
/// results moves on to the next. If a provider failed and none had results, the
/// result is <see cref="ErrorType.Unavailable"/>.
/// </summary>
public interface IBookMetadataService
{
    /// <summary>
    /// A valid ISBN-10/13 (hyphens allowed) is looked up as an ISBN; anything 
    /// else is a text search.  Values longer than the book's limits and invalid
    /// ISBNs are dropped from the results.
    /// </summary>
    Task<Result<IReadOnlyList<BookMetadata>>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads <see cref="BookMetadata.CoverImageUrl"/> through the provider 
    /// that returned it.
    /// </summary>
    Task<Result<CoverInput>> DownloadCoverAsync(
        BookMetadata book,
        CancellationToken cancellationToken = default);
}
