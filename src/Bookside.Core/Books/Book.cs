using Bookside.Core.Common;

namespace Bookside.Core.Books;

public class Book : Entity
{
    public const int TitleMaxLength = 500;
    public const int SubtitleMaxLength = 500;
    public const int PublisherMaxLength = 300;
    public const int DescriptionMaxLength = 10_000;
    public const int MetadataSourceMaxLength = 50;
    public const int MetadataSourceIdMaxLength = 100;

    public required string Title { get; set; }

    public string? Subtitle { get; set; }

    public int? PageCount { get; set; }

    /// <summary>
    /// Digits only, no hyphens.
    /// </summary>
    public string? Isbn13 { get; set; }

    /// <summary>
    /// Digits (and a trailing X), no hyphens.
    /// </summary>
    public string? Isbn10 { get; set; }

    public string? Publisher { get; set; }

    public int? PublishedYear { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Where imported metadata came from, e.g. "openlibrary".
    /// Null for manual entry.
    /// </summary>
    public string? MetadataSource { get; set; }

    /// <summary>
    /// The source's identifier, e.g. an Open Library edition key.
    /// </summary>
    public string? MetadataSourceId { get; set; }

    /// <summary>
    /// Ordered by <see cref="BookAuthor.Position"/>.
    /// </summary>
    public ICollection<BookAuthor> Authors { get; } = [];

    /// <summary>
    /// Times read = sessions with <see cref="ReadingStatus.Finished"/>;
    /// computed in queries, not stored.
    /// </summary>
    public ICollection<ReadingSession> ReadingSessions { get; } = [];

    public ICollection<BookComment> Comments { get; } = [];

    /// <summary>
    /// Stored in its own table so loading a book never drags the image bytes along.
    /// </summary>
    public BookCover? Cover { get; set; }
}
