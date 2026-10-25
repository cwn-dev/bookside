namespace Bookside.Core.Books.Services;

/// <summary>
/// Create/replace payload for a book. Strings are trimmed; blank optional
/// strings become null.
/// </summary>
public sealed record BookInput
{
    public required string Title { get; init; }

    public string? Subtitle { get; init; }

    /// <summary>
    /// Series name. Matched to an existing series case-insensitively (ASCII);
    /// a new name creates one.
    /// </summary>
    public string? Series { get; init; }

    /// <summary>
    /// Only allowed with <see cref="Series"/>; 0 to
    /// <see cref="Book.MaxSeriesNumber"/>.
    /// </summary>
    public double? SeriesNumber { get; init; }

    /// <summary>
    /// In display order. Matched to existing authors case-insensitively
    /// (ASCII); new names create authors.
    /// </summary>
    public IReadOnlyList<string> Authors { get; init; } = [];

    public int? PageCount { get; init; }

    /// <summary>
    /// Hyphens/spaces allowed. If only <see cref="Isbn10"/> is given, the
    /// ISBN-13 is derived from it.
    /// </summary>
    public string? Isbn13 { get; init; }

    public string? Isbn10 { get; init; }

    public string? Publisher { get; init; }

    public int? PublishedYear { get; init; }

    public string? Description { get; init; }

    public string? MetadataSource { get; init; }

    public string? MetadataSourceId { get; init; }
}

public enum BookFilter
{
    All = 0,

    /// <summary>
    /// Has a session with status Reading.
    /// </summary>
    Reading = 1,

    /// <summary>
    /// Finished at least once.
    /// </summary>
    Finished = 2,

    /// <summary>
    /// No reading sessions at all.
    /// </summary>
    Unread = 3,
}

public enum BookSort
{
    /// <summary>
    /// SQLite default collation: case-sensitive (see PROJECT_STATE open
    /// questions).
    /// </summary>
    Title = 0,
    RecentlyAdded = 1,
    RecentlyFinished = 2,
}

public sealed record BookQuery
{
    /// <summary>
    /// Substring match on title, subtitle, series or author name
    /// (case-insensitive for ASCII).
    /// </summary>
    public string? Search { get; init; }

    public BookFilter Filter { get; init; } = BookFilter.All;

    /// <summary>
    /// Only books with this tag (name, case-insensitive). Combined with
    /// <see cref="Search"/> and <see cref="Filter"/>.
    /// </summary>
    public string? Tag { get; init; }

    public BookSort Sort { get; init; } = BookSort.Title;

    public int Skip { get; init; }

    public int Take { get; init; } = Common.Paging.DefaultTake;
}

public sealed record BookSummary
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    public string? Subtitle { get; init; }

    public required IReadOnlyList<string> Authors { get; init; }

    public string? Series { get; init; }

    public double? SeriesNumber { get; init; }

    /// <summary>
    /// Tag names, alphabetical.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Null when there is no cover; otherwise the ETag / cache-busting key
    /// for the image.
    /// </summary>
    public string? CoverSha256 { get; init; }

    public int TimesRead { get; init; }

    public bool IsReading { get; init; }

    public DateOnly? LastFinishedOn { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}

public sealed record BookDetails
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    public string? Subtitle { get; init; }

    public required IReadOnlyList<string> Authors { get; init; }

    public string? Series { get; init; }

    public double? SeriesNumber { get; init; }

    /// <summary>
    /// Tag names, alphabetical.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    public int? PageCount { get; init; }

    public string? Isbn13 { get; init; }

    public string? Isbn10 { get; init; }

    public string? Publisher { get; init; }

    public int? PublishedYear { get; init; }

    public string? Description { get; init; }

    public string? MetadataSource { get; init; }

    public string? MetadataSourceId { get; init; }

    public string? CoverSha256 { get; init; }

    /// <summary>
    /// Newest first (by start date, then creation).
    /// </summary>
    public required IReadOnlyList<ReadingSessionDto> ReadingSessions { get; init; }

    public int TimesRead => ReadingSessions.Count(s => s.Status == ReadingStatus.Finished);

    /// <summary>
    /// Newest first (by creation).
    /// </summary>
    public IReadOnlyList<BookCommentDto> Comments { get; init; } = [];

    public int VocabularyWordCount { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}

public sealed record ReadingSessionInput
{
    public DateOnly? StartedOn { get; init; }

    /// <summary>
    /// Only allowed when <see cref="Status"/> is Finished or Abandoned;
    /// must not precede <see cref="StartedOn"/>.
    /// </summary>
    public DateOnly? FinishedOn { get; init; }

    public ReadingStatus Status { get; init; } = ReadingStatus.Reading;

    public int? Rating { get; init; }

    public string? Notes { get; init; }
}

public sealed record ReadingSessionDto
{
    public required Guid Id { get; init; }

    public required Guid BookId { get; init; }

    public DateOnly? StartedOn { get; init; }

    public DateOnly? FinishedOn { get; init; }

    public ReadingStatus Status { get; init; }

    public int? Rating { get; init; }

    public string? Notes { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}

public sealed record CoverInput
{
    public required ReadOnlyMemory<byte> Content { get; init; }

    /// <summary>
    /// One of <see cref="BookCover.AllowedContentTypes"/>.
    /// </summary>
    public required string ContentType { get; init; }

    public string? SourceUrl { get; init; }
}

public sealed record CoverContent
{
    public required ReadOnlyMemory<byte> Content { get; init; }

    public required string ContentType { get; init; }

    /// <summary>
    /// Hex SHA-256; use as the HTTP ETag.
    /// </summary>
    public required string Sha256 { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}

public sealed record BookCommentInput
{
    /// <summary>
    /// Required; trimmed. Plain text, line breaks kept.
    /// </summary>
    public required string Text { get; init; }
}

public sealed record BookCommentDto
{
    public required Guid Id { get; init; }

    public required Guid BookId { get; init; }

    public required string Text { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }

    /// <summary>
    /// Saving unchanged text doesn't count as an edit.
    /// </summary>
    public bool IsEdited => UpdatedAtUtc > CreatedAtUtc;
}
