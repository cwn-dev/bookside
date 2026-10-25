using Bookside.Core.Common;

namespace Bookside.Core.Books.Services;

/// <summary>Books, authors, covers, reading sessions and comments. Expected failures come back as <see cref="Result"/> errors.</summary>
public interface ILibraryService
{
    Task<PagedResult<BookSummary>> ListBooksAsync(BookQuery query, CancellationToken cancellationToken = default);

    Task<Result<BookDetails>> GetBookAsync(Guid bookId, CancellationToken cancellationToken = default);

    Task<Result<BookDetails>> AddBookAsync(BookInput input, CancellationToken cancellationToken = default);

    /// <summary>Replaces all editable fields, including the author list. Authors and series left without books are deleted.</summary>
    Task<Result<BookDetails>> UpdateBookAsync(Guid bookId, BookInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes sessions, comments and cover with the book; vocabulary words are kept and unlinked.</summary>
    Task<Result> DeleteBookAsync(Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>All series names, alphabetically (for the book form's picker).</summary>
    Task<IReadOnlyList<string>> ListSeriesNamesAsync(CancellationToken cancellationToken = default);

    Task<Result<CoverContent>> GetCoverAsync(Guid bookId, CancellationToken cancellationToken = default);

    Task<Result> SetCoverAsync(Guid bookId, CoverInput cover, CancellationToken cancellationToken = default);

    Task<Result> RemoveCoverAsync(Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>A book can have at most one session with status Reading.</summary>
    Task<Result<ReadingSessionDto>> AddReadingSessionAsync(Guid bookId, ReadingSessionInput input, CancellationToken cancellationToken = default);

    Task<Result<ReadingSessionDto>> UpdateReadingSessionAsync(Guid sessionId, ReadingSessionInput input, CancellationToken cancellationToken = default);

    Task<Result> DeleteReadingSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<Result<BookCommentDto>> AddCommentAsync(Guid bookId, BookCommentInput input, CancellationToken cancellationToken = default);

    Task<Result<BookCommentDto>> UpdateCommentAsync(Guid commentId, BookCommentInput input, CancellationToken cancellationToken = default);

    Task<Result> DeleteCommentAsync(Guid commentId, CancellationToken cancellationToken = default);
}
