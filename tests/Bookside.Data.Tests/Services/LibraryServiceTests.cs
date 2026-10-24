using System.Security.Cryptography;
using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.Core.Common;
using Bookside.Core.Vocabulary.Services;
using Microsoft.EntityFrameworkCore;

namespace Bookside.Data.Tests.Services;

public sealed class LibraryServiceTests : ServiceTestBase
{
    [Fact]
    public async Task Adds_a_book_with_ordered_authors_and_cleaned_fields()
    {
        var book = Success(await Library.AddBookAsync(
            new BookInput
            {
                Title = "  Good Omens ",
                Subtitle = "   ",
                Authors = ["Terry Pratchett", " Neil Gaiman ", "", "terry pratchett"],
                Isbn10 = "0-06-085398-0",
                PageCount = 412,
            },
            Ct));

        Assert.Equal("Good Omens", book.Title);
        Assert.Null(book.Subtitle);
        Assert.Equal(["Terry Pratchett", "Neil Gaiman"], book.Authors);
        Assert.Equal("0060853980", book.Isbn10);
        Assert.Equal("9780060853983", book.Isbn13); // derived from the ISBN-10
        Assert.Empty(book.ReadingSessions);
        Assert.NotEqual(default, book.CreatedAtUtc);
    }

    [Fact]
    public async Task Invalid_book_input_returns_every_field_error()
    {
        var result = await Library.AddBookAsync(
            new BookInput { Title = " ", Isbn13 = "9780060853984", PageCount = 0, PublishedYear = 99_999 },
            Ct);

        var error = Failure(result, ErrorType.Validation);
        Assert.Equal(ErrorCodes.Validation, error.Code);
        Assert.Equal(
            ["Isbn13", "PageCount", "PublishedYear", "Title"],
            error.FieldErrors.Select(e => e.Field).Order());
    }

    [Fact]
    public async Task Existing_authors_are_reused_case_insensitively()
    {
        await AddBookAsync("Mort", "Terry Pratchett");
        await AddBookAsync("Small Gods", "TERRY PRATCHETT");

        await using var db = Database.CreateContext();
        var author = await db.Authors.SingleAsync(Ct);
        Assert.Equal("Terry Pratchett", author.Name);
        Assert.Equal(2, await db.BookAuthors.CountAsync(Ct));
    }

    [Fact]
    public async Task Update_reorders_authors_and_deletes_orphans()
    {
        var book = await AddBookAsync("Good Omens", "Terry Pratchett", "Neil Gaiman", "Typo Author");

        var updated = Success(await Library.UpdateBookAsync(
            book.Id,
            new BookInput { Title = "Good Omens", Authors = ["Neil Gaiman", "Terry Pratchett"] },
            Ct));

        Assert.Equal(["Neil Gaiman", "Terry Pratchett"], updated.Authors);
        await using var db = Database.CreateContext();
        Assert.Equal(["Neil Gaiman", "Terry Pratchett"], await db.Authors.Select(a => a.Name).Order().ToListAsync(Ct));
    }

    [Fact]
    public async Task Unknown_ids_return_not_found()
    {
        var id = Guid.CreateVersion7();

        Assert.Equal(ErrorCodes.BookNotFound, Failure(await Library.GetBookAsync(id, Ct), ErrorType.NotFound).Code);
        Assert.Equal(ErrorCodes.BookNotFound, Failure(await Library.UpdateBookAsync(id, new BookInput { Title = "x" }, Ct), ErrorType.NotFound).Code);
        Assert.Equal(ErrorCodes.BookNotFound, Failure(await Library.DeleteBookAsync(id, Ct), ErrorType.NotFound).Code);
        Assert.Equal(ErrorCodes.CoverNotFound, Failure(await Library.GetCoverAsync(id, Ct), ErrorType.NotFound).Code);
        Assert.Equal(ErrorCodes.ReadingSessionNotFound, Failure(await Library.DeleteReadingSessionAsync(id, Ct), ErrorType.NotFound).Code);
    }

    [Fact]
    public async Task Delete_removes_sessions_cover_and_orphaned_authors_but_keeps_words()
    {
        var book = await AddBookAsync("Mort", "Terry Pratchett");
        await AddBookAsync("Coraline", "Neil Gaiman");
        Success(await Library.AddReadingSessionAsync(book.Id, new ReadingSessionInput(), Ct));
        Assert.True((await Library.SetCoverAsync(book.Id, new CoverInput { Content = new byte[] { 1, 2, 3 }, ContentType = "image/png" }, Ct)).IsSuccess);
        var word = Success(await Vocabulary.AddWordAsync(new WordInput { Term = "psychopomp", BookId = book.Id }, Ct));

        Assert.True((await Library.DeleteBookAsync(book.Id, Ct)).IsSuccess);

        await using var db = Database.CreateContext();
        Assert.Empty(await db.ReadingSessions.ToListAsync(Ct));
        Assert.Empty(await db.BookCovers.ToListAsync(Ct));
        Assert.Equal(["Neil Gaiman"], await db.Authors.Select(a => a.Name).ToListAsync(Ct));
        Assert.Null((await db.VocabularyWords.SingleAsync(w => w.Id == word.Id, Ct)).BookId);
    }

    [Fact]
    public async Task Search_matches_title_subtitle_and_author_ignoring_ascii_case()
    {
        await AddBookAsync("Mort", "Terry Pratchett");
        Success(await Library.AddBookAsync(new BookInput { Title = "Dune", Subtitle = "Book One" }, Ct));
        await AddBookAsync("100% Wolf");
        await AddBookAsync("1000 Wolves");

        Assert.Equal(["Mort"], await SearchAsync("PRATCH"));
        Assert.Equal(["Dune"], await SearchAsync("book one"));
        Assert.Equal(["100% Wolf"], await SearchAsync("100%")); // wildcard characters are literal
        Assert.Empty(await SearchAsync("nothing"));

        async Task<IEnumerable<string>> SearchAsync(string text) =>
            (await Library.ListBooksAsync(new BookQuery { Search = text }, Ct)).Items.Select(b => b.Title);
    }

    [Fact]
    public async Task List_filters_sorts_and_pages()
    {
        var reading = await AddBookAsync("B Reading", "Author");
        var finished = await AddBookAsync("C Finished");
        await AddBookAsync("A Unread");
        Success(await Library.AddReadingSessionAsync(reading.Id, new ReadingSessionInput { StartedOn = new DateOnly(2026, 10, 1) }, Ct));
        Success(await Library.AddReadingSessionAsync(finished.Id, Finished(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 9)), Ct));
        Success(await Library.AddReadingSessionAsync(finished.Id, Finished(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5)), Ct));

        var all = await Library.ListBooksAsync(new BookQuery(), Ct);
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(["A Unread", "B Reading", "C Finished"], all.Items.Select(b => b.Title));

        var finishedSummary = Assert.Single((await Library.ListBooksAsync(new BookQuery { Filter = BookFilter.Finished }, Ct)).Items);
        Assert.Equal(2, finishedSummary.TimesRead);
        Assert.Equal(new DateOnly(2026, 3, 5), finishedSummary.LastFinishedOn);

        var readingSummary = Assert.Single((await Library.ListBooksAsync(new BookQuery { Filter = BookFilter.Reading }, Ct)).Items);
        Assert.True(readingSummary.IsReading);
        Assert.Equal(["Author"], readingSummary.Authors);

        Assert.Equal("A Unread", Assert.Single((await Library.ListBooksAsync(new BookQuery { Filter = BookFilter.Unread }, Ct)).Items).Title);

        var page = await Library.ListBooksAsync(new BookQuery { Sort = BookSort.RecentlyFinished, Skip = 1, Take = 1 }, Ct);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal("A Unread", Assert.Single(page.Items).Title); // never finished, sorted after; ties by title
    }

    [Fact]
    public async Task Cover_is_hashed_replaced_and_removed()
    {
        var book = await AddBookAsync("Mort");
        byte[] first = [0xFF, 0xD8, 0xFF];
        byte[] second = [0x89, 0x50, 0x4E, 0x47];

        Assert.True((await Library.SetCoverAsync(book.Id, new CoverInput { Content = first, ContentType = "image/JPEG" }, Ct)).IsSuccess);
        Assert.True((await Library.SetCoverAsync(book.Id, new CoverInput { Content = second, ContentType = "image/png" }, Ct)).IsSuccess);

        var cover = Success(await Library.GetCoverAsync(book.Id, Ct));
        Assert.Equal(second, cover.Content.ToArray());
        Assert.Equal("image/png", cover.ContentType);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(second)), cover.Sha256);
        Assert.Equal(cover.Sha256, Success(await Library.GetBookAsync(book.Id, Ct)).CoverSha256);

        Assert.True((await Library.RemoveCoverAsync(book.Id, Ct)).IsSuccess);
        Failure(await Library.GetCoverAsync(book.Id, Ct), ErrorType.NotFound);
    }

    [Fact]
    public async Task Cover_rejects_unsupported_type_and_oversized_images()
    {
        var book = await AddBookAsync("Mort");

        var error = Failure(
            await Library.SetCoverAsync(book.Id, new CoverInput { Content = new byte[BookCover.MaxSizeBytes + 1], ContentType = "application/pdf" }, Ct),
            ErrorType.Validation);

        Assert.Equal(["Content", "ContentType"], error.FieldErrors.Select(e => e.Field).Order());
    }

    [Fact]
    public async Task Only_one_session_can_be_in_progress()
    {
        var book = await AddBookAsync("Mort");
        var first = Success(await Library.AddReadingSessionAsync(book.Id, new ReadingSessionInput(), Ct));

        var conflict = Failure(await Library.AddReadingSessionAsync(book.Id, new ReadingSessionInput(), Ct), ErrorType.Conflict);
        Assert.Equal(ErrorCodes.AlreadyReading, conflict.Code);

        // Finishing the first one frees the slot; re-saving an in-progress session is not a conflict with itself.
        Success(await Library.UpdateReadingSessionAsync(first.Id, new ReadingSessionInput { Status = ReadingStatus.Finished, Rating = 4 }, Ct));
        var second = Success(await Library.AddReadingSessionAsync(book.Id, new ReadingSessionInput(), Ct));
        Success(await Library.UpdateReadingSessionAsync(second.Id, new ReadingSessionInput { Notes = "  re-read  " }, Ct));

        var details = Success(await Library.GetBookAsync(book.Id, Ct));
        Assert.Equal(1, details.TimesRead);
        Assert.Contains(details.ReadingSessions, s => s.Notes == "re-read");
    }

    [Fact]
    public async Task Invalid_session_input_returns_field_errors()
    {
        var book = await AddBookAsync("Mort");

        var inProgressWithFinishDate = Failure(
            await Library.AddReadingSessionAsync(book.Id, new ReadingSessionInput { FinishedOn = new DateOnly(2026, 1, 1) }, Ct),
            ErrorType.Validation);
        Assert.Equal("FinishedOn", Assert.Single(inProgressWithFinishDate.FieldErrors).Field);

        var backwardsAndBadRating = Failure(
            await Library.AddReadingSessionAsync(
                book.Id,
                Finished(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)) with { Rating = 6 },
                Ct),
            ErrorType.Validation);
        Assert.Equal(["FinishedOn", "Rating"], backwardsAndBadRating.FieldErrors.Select(e => e.Field).Order());
    }

    [Fact]
    public async Task Deleting_a_session_removes_it_from_the_book()
    {
        var book = await AddBookAsync("Mort");
        var session = Success(await Library.AddReadingSessionAsync(book.Id, new ReadingSessionInput(), Ct));

        Assert.True((await Library.DeleteReadingSessionAsync(session.Id, Ct)).IsSuccess);
        Assert.Empty(Success(await Library.GetBookAsync(book.Id, Ct)).ReadingSessions);
    }

    private static ReadingSessionInput Finished(DateOnly started, DateOnly finished) =>
        new() { StartedOn = started, FinishedOn = finished, Status = ReadingStatus.Finished };
}
