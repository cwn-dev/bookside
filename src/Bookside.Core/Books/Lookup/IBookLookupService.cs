using Bookside.Core.Books.Services;

namespace Bookside.Core.Books.Lookup;

/// <summary>
/// One book metadata provider (Open Library, Google Books). Several can be
/// registered; <see cref="IBookMetadataService"/> tries them in registration
/// order. Failures throw <see cref="BookLookupException"/>.
/// </summary>
public interface IBookLookupService
{
    /// <summary>
    /// Provider id stored in <see cref="Book.MetadataSource"/>, e.g. "openlibrary".
    /// </summary>
    string Source { get; }

    /// <summary>
    /// Returns null when the provider has no record for the ISBN.
    /// </summary>
    /// <param name="isbn13">Clean, checksum-valid ISBN-13.</param>
    Task<BookMetadata?> FindByIsbnAsync(
        string isbn13,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Free-text search (title and/or author); empty when nothing matches.
    /// </summary>
    Task<IReadOnlyList<BookMetadata>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a <see cref="BookMetadata.CoverImageUrl"/> this provider
    /// returned. Other hosts are refused.
    /// </summary>
    Task<CoverInput> DownloadCoverAsync(
        Uri url,
        CancellationToken cancellationToken = default);
}
