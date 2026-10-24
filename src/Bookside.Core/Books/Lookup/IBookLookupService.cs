namespace Bookside.Core.Books.Lookup;

/// <summary>
/// External book metadata lookup (roadmap: Open Library implementation, Google Books as optional fallback).
/// No implementation yet.
/// </summary>
public interface IBookLookupService
{
    /// <summary>Provider id stored in <see cref="Book.MetadataSource"/>, e.g. "openlibrary".</summary>
    string Source { get; }

    /// <summary>Returns null when the provider has no record for the ISBN.</summary>
    Task<BookMetadata?> FindByIsbnAsync(string isbn, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookMetadata>> SearchAsync(string query, int maxResults = 20, CancellationToken cancellationToken = default);
}
