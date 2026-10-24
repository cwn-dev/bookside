using System.Security.Cryptography;
using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace Bookside.Data.Services;

/// <summary>Each call uses its own short-lived context, so the service is safe for a whole Blazor circuit.</summary>
internal sealed class LibraryService(IDbContextFactory<BooksideDbContext> dbFactory, TimeProvider timeProvider) : ILibraryService
{
    private const int MinPublishedYear = -3000;

    public async Task<PagedResult<BookSummary>> ListBooksAsync(BookQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (skip, take) = Paging.Clamp(query.Skip, query.Take);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var books = db.Books.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Contains(query.Search.Trim());
            books = books.Where(b =>
                EF.Functions.Like(b.Title, pattern, LikePattern.EscapeCharacter)
                || (b.Subtitle != null && EF.Functions.Like(b.Subtitle, pattern, LikePattern.EscapeCharacter))
                || b.Authors.Any(a => EF.Functions.Like(a.Author.Name, pattern, LikePattern.EscapeCharacter)));
        }

        books = query.Filter switch
        {
            BookFilter.Reading => books.Where(b => b.ReadingSessions.Any(s => s.Status == ReadingStatus.Reading)),
            BookFilter.Finished => books.Where(b => b.ReadingSessions.Any(s => s.Status == ReadingStatus.Finished)),
            BookFilter.Unread => books.Where(b => !b.ReadingSessions.Any()),
            _ => books,
        };

        var total = await books.CountAsync(cancellationToken).ConfigureAwait(false);

        var summaries = books.Select(b => new BookSummary
        {
            Id = b.Id,
            Title = b.Title,
            Subtitle = b.Subtitle,
            Authors = b.Authors.OrderBy(a => a.Position).Select(a => a.Author.Name).ToList(),
            CoverSha256 = b.Cover!.Sha256,
            TimesRead = b.ReadingSessions.Count(s => s.Status == ReadingStatus.Finished),
            IsReading = b.ReadingSessions.Any(s => s.Status == ReadingStatus.Reading),
            LastFinishedOn = b.ReadingSessions.Where(s => s.Status == ReadingStatus.Finished).Max(s => s.FinishedOn),
            CreatedAtUtc = b.CreatedAtUtc,
        });

        summaries = query.Sort switch
        {
            BookSort.RecentlyAdded => summaries.OrderByDescending(b => b.CreatedAtUtc).ThenBy(b => b.Id),
            BookSort.RecentlyFinished => summaries.OrderByDescending(b => b.LastFinishedOn).ThenBy(b => b.Title).ThenBy(b => b.Id),
            _ => summaries.OrderBy(b => b.Title).ThenBy(b => b.Id),
        };

        var items = await summaries.Skip(skip).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResult<BookSummary>(items, total);
    }

    public async Task<Result<BookDetails>> GetBookAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await LoadDetailsAsync(db, bookId, cancellationToken).ConfigureAwait(false) is { } details
            ? details
            : BookNotFound(bookId);
    }

    public async Task<Result<BookDetails>> AddBookAsync(BookInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var book = new Book { Title = string.Empty };
        var validator = new InputValidator();
        var authorNames = ApplyInput(validator, input, book);
        if (!validator.IsValid)
        {
            return validator.ToError();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.Books.Add(book);
        await SetAuthorsAsync(db, book, authorNames, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (await LoadDetailsAsync(db, book.Id, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<Result<BookDetails>> UpdateBookAsync(Guid bookId, BookInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var book = await db.Books
            .Include(b => b.Authors).ThenInclude(ba => ba.Author)
            .SingleOrDefaultAsync(b => b.Id == bookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is null)
        {
            return BookNotFound(bookId);
        }

        var validator = new InputValidator();
        var authorNames = ApplyInput(validator, input, book);
        if (!validator.IsValid)
        {
            return validator.ToError();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var removedAuthorIds = await SetAuthorsAsync(db, book, authorNames, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await DeleteOrphanedAuthorsAsync(db, removedAuthorIds, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return (await LoadDetailsAsync(db, bookId, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<Result> DeleteBookAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var authorIds = await db.BookAuthors
            .Where(ba => ba.BookId == bookId)
            .Select(ba => ba.AuthorId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Sessions, cover and author links cascade in the database; vocabulary words are set to null.
        var deleted = await db.Books.Where(b => b.Id == bookId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        if (deleted == 0)
        {
            return BookNotFound(bookId);
        }

        await DeleteOrphanedAuthorsAsync(db, authorIds, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<CoverContent>> GetCoverAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var cover = await db.BookCovers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.BookId == bookId, cancellationToken)
            .ConfigureAwait(false);

        if (cover is null)
        {
            return ServiceError.NotFound(ErrorCodes.CoverNotFound, $"Book {bookId} has no cover.");
        }

        return new CoverContent
        {
            Content = cover.Content,
            ContentType = cover.ContentType,
            Sha256 = cover.Sha256,
            UpdatedAtUtc = cover.UpdatedAtUtc,
        };
    }

    public async Task<Result> SetCoverAsync(Guid bookId, CoverInput cover, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cover);
        var validator = new InputValidator();
        validator.Check(!cover.Content.IsEmpty, nameof(CoverInput.Content), "Image is empty.");
        validator.Check(
            cover.Content.Length <= BookCover.MaxSizeBytes,
            nameof(CoverInput.Content),
            $"Image is larger than {BookCover.MaxSizeBytes / 1024 / 1024} MB.");
        var contentType = validator.Required(nameof(CoverInput.ContentType), cover.ContentType, 100).ToLowerInvariant();
        validator.Check(
            contentType.Length == 0 || BookCover.AllowedContentTypes.Contains(contentType),
            nameof(CoverInput.ContentType),
            "Unsupported image type.");
        var sourceUrl = validator.Optional(nameof(CoverInput.SourceUrl), cover.SourceUrl, BookCover.SourceUrlMaxLength);
        if (!validator.IsValid)
        {
            return validator.ToError();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (!await db.Books.AnyAsync(b => b.Id == bookId, cancellationToken).ConfigureAwait(false))
        {
            return BookNotFound(bookId);
        }

        var content = cover.Content.ToArray();
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
        var existing = await db.BookCovers.SingleOrDefaultAsync(c => c.BookId == bookId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            db.BookCovers.Add(new BookCover
            {
                BookId = bookId,
                Content = content,
                ContentType = contentType,
                Sha256 = sha256,
                SourceUrl = sourceUrl,
            });
        }
        else
        {
            existing.Content = content;
            existing.ContentType = contentType;
            existing.Sha256 = sha256;
            existing.SourceUrl = sourceUrl;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> RemoveCoverAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var deleted = await db.BookCovers.Where(c => c.BookId == bookId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        return deleted == 0
            ? ServiceError.NotFound(ErrorCodes.CoverNotFound, $"Book {bookId} has no cover.")
            : Result.Success();
    }

    public async Task<Result<ReadingSessionDto>> AddReadingSessionAsync(
        Guid bookId,
        ReadingSessionInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var session = new ReadingSession { BookId = bookId };
        if (ApplyInput(input, session) is { } invalid)
        {
            return invalid;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (!await db.Books.AnyAsync(b => b.Id == bookId, cancellationToken).ConfigureAwait(false))
        {
            return BookNotFound(bookId);
        }

        if (await ConflictsWithCurrentReadAsync(db, session, cancellationToken).ConfigureAwait(false) is { } conflict)
        {
            return conflict;
        }

        db.ReadingSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(session);
    }

    public async Task<Result<ReadingSessionDto>> UpdateReadingSessionAsync(
        Guid sessionId,
        ReadingSessionInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var session = await db.ReadingSessions.SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return SessionNotFound(sessionId);
        }

        if (ApplyInput(input, session) is { } invalid)
        {
            return invalid;
        }

        if (await ConflictsWithCurrentReadAsync(db, session, cancellationToken).ConfigureAwait(false) is { } conflict)
        {
            return conflict;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(session);
    }

    public async Task<Result> DeleteReadingSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var deleted = await db.ReadingSessions.Where(s => s.Id == sessionId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        return deleted == 0 ? SessionNotFound(sessionId) : Result.Success();
    }

    private static ServiceError BookNotFound(Guid bookId) =>
        ServiceError.NotFound(ErrorCodes.BookNotFound, $"Book {bookId} was not found.");

    private static ServiceError SessionNotFound(Guid sessionId) =>
        ServiceError.NotFound(ErrorCodes.ReadingSessionNotFound, $"Reading session {sessionId} was not found.");

    private static Task<BookDetails?> LoadDetailsAsync(BooksideDbContext db, Guid bookId, CancellationToken cancellationToken) =>
        db.Books.AsNoTracking()
            .Where(b => b.Id == bookId)
            .Select(b => new BookDetails
            {
                Id = b.Id,
                Title = b.Title,
                Subtitle = b.Subtitle,
                Authors = b.Authors.OrderBy(a => a.Position).Select(a => a.Author.Name).ToList(),
                PageCount = b.PageCount,
                Isbn13 = b.Isbn13,
                Isbn10 = b.Isbn10,
                Publisher = b.Publisher,
                PublishedYear = b.PublishedYear,
                Description = b.Description,
                MetadataSource = b.MetadataSource,
                MetadataSourceId = b.MetadataSourceId,
                CoverSha256 = b.Cover!.Sha256,
                ReadingSessions = b.ReadingSessions
                    .OrderByDescending(s => s.StartedOn)
                    .ThenByDescending(s => s.CreatedAtUtc)
                    .Select(s => new ReadingSessionDto
                    {
                        Id = s.Id,
                        BookId = s.BookId,
                        StartedOn = s.StartedOn,
                        FinishedOn = s.FinishedOn,
                        Status = s.Status,
                        Rating = s.Rating,
                        Notes = s.Notes,
                        CreatedAtUtc = s.CreatedAtUtc,
                        UpdatedAtUtc = s.UpdatedAtUtc,
                    })
                    .ToList(),
                VocabularyWordCount = db.VocabularyWords.Count(w => w.BookId == b.Id),
                CreatedAtUtc = b.CreatedAtUtc,
                UpdatedAtUtc = b.UpdatedAtUtc,
            })
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Validates <paramref name="input"/> onto <paramref name="book"/> and returns the cleaned author names.</summary>
    private List<string> ApplyInput(InputValidator v, BookInput input, Book book)
    {
        book.Title = v.Required(nameof(BookInput.Title), input.Title, Book.TitleMaxLength);
        book.Subtitle = v.Optional(nameof(BookInput.Subtitle), input.Subtitle, Book.SubtitleMaxLength);
        book.Publisher = v.Optional(nameof(BookInput.Publisher), input.Publisher, Book.PublisherMaxLength);
        book.Description = v.Optional(nameof(BookInput.Description), input.Description, Book.DescriptionMaxLength);
        book.MetadataSource = v.Optional(nameof(BookInput.MetadataSource), input.MetadataSource, Book.MetadataSourceMaxLength);
        book.MetadataSourceId = v.Optional(nameof(BookInput.MetadataSourceId), input.MetadataSourceId, Book.MetadataSourceIdMaxLength);

        v.Check(input.PageCount is null or > 0, nameof(BookInput.PageCount), "Must be greater than zero.");
        book.PageCount = input.PageCount;

        var maxYear = timeProvider.GetUtcNow().Year + 1;
        v.Check(
            input.PublishedYear is null || (input.PublishedYear >= MinPublishedYear && input.PublishedYear <= maxYear),
            nameof(BookInput.PublishedYear),
            $"Must be between {MinPublishedYear} and {maxYear}.");
        book.PublishedYear = input.PublishedYear;

        var isbn13 = CleanOrNull(input.Isbn13);
        var isbn10 = CleanOrNull(input.Isbn10);
        v.Check(isbn13 is null || Isbn.IsValidIsbn13(isbn13), nameof(BookInput.Isbn13), "Not a valid ISBN-13.");
        v.Check(isbn10 is null || Isbn.IsValidIsbn10(isbn10), nameof(BookInput.Isbn10), "Not a valid ISBN-10.");
        if (isbn13 is null && isbn10 is not null && Isbn.IsValidIsbn10(isbn10))
        {
            isbn13 = Isbn.ToIsbn13(isbn10);
        }

        book.Isbn13 = isbn13;
        book.Isbn10 = isbn10;

        var names = new List<string>();
        foreach (var raw in input.Authors ?? [])
        {
            var name = raw?.Trim();
            if (string.IsNullOrEmpty(name) || names.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            v.Check(name.Length <= Author.NameMaxLength, nameof(BookInput.Authors), $"Author names must be at most {Author.NameMaxLength} characters.");
            names.Add(name);
        }

        return names;

        static string? CleanOrNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : Isbn.Clean(value);
    }

    /// <summary>Rewrites the book's author links in order and returns the ids of authors that were unlinked.</summary>
    private static async Task<List<Guid>> SetAuthorsAsync(
        BooksideDbContext db,
        Book book,
        List<string> names,
        CancellationToken cancellationToken)
    {
        var desired = new List<Author>(names.Count);
        foreach (var name in names)
        {
            var author = book.Authors.Select(ba => ba.Author).FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? await db.Authors
                    .Where(a => EF.Functions.Like(a.Name, LikePattern.Exact(name), LikePattern.EscapeCharacter))
                    .OrderBy(a => a.CreatedAtUtc)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (author is null)
            {
                author = new Author { Name = name };
                db.Authors.Add(author);
            }

            desired.Add(author);
        }

        var removed = new List<Guid>();
        foreach (var link in book.Authors.ToList())
        {
            if (!desired.Exists(a => a.Id == link.AuthorId))
            {
                book.Authors.Remove(link);
                db.BookAuthors.Remove(link);
                removed.Add(link.AuthorId);
            }
        }

        for (var position = 0; position < desired.Count; position++)
        {
            var author = desired[position];
            var link = book.Authors.FirstOrDefault(ba => ba.AuthorId == author.Id);
            if (link is null)
            {
                link = new BookAuthor { Book = book, BookId = book.Id, Author = author, AuthorId = author.Id };
                db.BookAuthors.Add(link);
                book.Authors.Add(link);
            }

            link.Position = position;
        }

        return removed;
    }

    private static async Task DeleteOrphanedAuthorsAsync(BooksideDbContext db, List<Guid> authorIds, CancellationToken cancellationToken)
    {
        if (authorIds.Count == 0)
        {
            return;
        }

        await db.Authors
            .Where(a => authorIds.Contains(a.Id) && !a.Books.Any())
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Validates onto <paramref name="session"/>; returns the error, or null when valid.</summary>
    private static ServiceError? ApplyInput(ReadingSessionInput input, ReadingSession session)
    {
        var v = new InputValidator();
        v.Check(Enum.IsDefined(input.Status), nameof(ReadingSessionInput.Status), "Unknown status.");
        v.Check(
            input.Rating is null || (input.Rating >= ReadingSession.MinRating && input.Rating <= ReadingSession.MaxRating),
            nameof(ReadingSessionInput.Rating),
            $"Must be between {ReadingSession.MinRating} and {ReadingSession.MaxRating}.");
        v.Check(
            input.FinishedOn is null || input.Status != ReadingStatus.Reading,
            nameof(ReadingSessionInput.FinishedOn),
            "A session still being read has no finish date.");
        v.Check(
            input.StartedOn is null || input.FinishedOn is null || input.FinishedOn >= input.StartedOn,
            nameof(ReadingSessionInput.FinishedOn),
            "Cannot be before the start date.");
        var notes = v.Optional(nameof(ReadingSessionInput.Notes), input.Notes, ReadingSession.NotesMaxLength);
        if (!v.IsValid)
        {
            return v.ToError();
        }

        session.StartedOn = input.StartedOn;
        session.FinishedOn = input.FinishedOn;
        session.Status = input.Status;
        session.Rating = input.Rating;
        session.Notes = notes;
        return null;
    }

    private static async Task<ServiceError?> ConflictsWithCurrentReadAsync(
        BooksideDbContext db,
        ReadingSession session,
        CancellationToken cancellationToken)
    {
        if (session.Status != ReadingStatus.Reading)
        {
            return null;
        }

        var alreadyReading = await db.ReadingSessions
            .AnyAsync(s => s.BookId == session.BookId && s.Id != session.Id && s.Status == ReadingStatus.Reading, cancellationToken)
            .ConfigureAwait(false);

        return alreadyReading
            ? ServiceError.Conflict(ErrorCodes.AlreadyReading, "This book already has a reading session in progress.")
            : null;
    }

    private static ReadingSessionDto ToDto(ReadingSession s) => new()
    {
        Id = s.Id,
        BookId = s.BookId,
        StartedOn = s.StartedOn,
        FinishedOn = s.FinishedOn,
        Status = s.Status,
        Rating = s.Rating,
        Notes = s.Notes,
        CreatedAtUtc = s.CreatedAtUtc,
        UpdatedAtUtc = s.UpdatedAtUtc,
    };
}
