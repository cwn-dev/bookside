namespace Bookside.Core.Books.Lookup;

/// <summary>Provider-neutral metadata returned by <see cref="IBookLookupService"/>, ready to map onto a <see cref="Book"/>.</summary>
public sealed record BookMetadata
{
    public required string Source { get; init; }

    public required string SourceId { get; init; }

    public required string Title { get; init; }

    public string? Subtitle { get; init; }

    public IReadOnlyList<string> Authors { get; init; } = [];

    public int? PageCount { get; init; }

    public string? Isbn13 { get; init; }

    public string? Isbn10 { get; init; }

    public string? Publisher { get; init; }

    public int? PublishedYear { get; init; }

    public string? Description { get; init; }

    /// <summary>Download URL; the importer fetches the bytes into a <see cref="BookCover"/>.</summary>
    public Uri? CoverImageUrl { get; init; }
}
