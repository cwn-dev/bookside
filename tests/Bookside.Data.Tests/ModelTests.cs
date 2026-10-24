using System.Security.Cryptography;
using Bookside.Core.Books;
using Bookside.Core.Vocabulary;
using Microsoft.EntityFrameworkCore;

namespace Bookside.Data.Tests;

public sealed class ModelTests : IAsyncLifetime
{
    private readonly SqliteTestDatabase _database = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => _database.InitializeAsync();

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task Round_trips_a_full_book_graph()
    {
        var coverBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 };
        var book = new Book { Title = "Good Omens", PageCount = 412, Isbn13 = "9780060853983" };
        book.Authors.Add(new BookAuthor { Author = new Author { Name = "Terry Pratchett" }, Position = 0 });
        book.Authors.Add(new BookAuthor { Author = new Author { Name = "Neil Gaiman" }, Position = 1 });
        book.Cover = new BookCover
        {
            Content = coverBytes,
            ContentType = "image/jpeg",
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(coverBytes)),
        };
        book.ReadingSessions.Add(new ReadingSession
        {
            StartedOn = new DateOnly(2019, 6, 1),
            FinishedOn = new DateOnly(2019, 6, 14),
            Status = ReadingStatus.Finished,
            Rating = 5,
        });
        book.ReadingSessions.Add(new ReadingSession { StartedOn = new DateOnly(2026, 10, 1) });

        await using (var db = _database.CreateContext())
        {
            db.Books.Add(book);
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = _database.CreateContext())
        {
            var loaded = await db.Books
                .Include(b => b.Authors.OrderBy(a => a.Position)).ThenInclude(a => a.Author)
                .Include(b => b.ReadingSessions)
                .SingleAsync(b => b.Id == book.Id, Ct);

            Assert.Equal(["Terry Pratchett", "Neil Gaiman"], loaded.Authors.Select(a => a.Author.Name));
            Assert.Equal(1, loaded.ReadingSessions.Count(s => s.Status == ReadingStatus.Finished));
            Assert.Equal(DateTimeKind.Utc, loaded.CreatedAtUtc.Kind);
            Assert.NotEqual(default, loaded.UpdatedAtUtc);
            Assert.Null(loaded.Cover); // not loaded unless asked for

            var cover = await db.BookCovers.SingleAsync(c => c.BookId == book.Id, Ct);
            Assert.Equal(coverBytes, cover.Content);
        }
    }

    [Fact]
    public async Task Stores_dictionary_meanings_as_json_and_links_words()
    {
        var entry = new DictionaryEntry
        {
            NormalizedTerm = "Petrichor",
            Language = "en",
            Source = "dictionaryapi.dev",
            Status = DictionaryLookupStatus.Found,
            FetchedAtUtc = DateTime.UtcNow,
            Meanings =
            [
                new DictionaryMeaning
                {
                    PartOfSpeech = "noun",
                    Definitions = [new DictionaryDefinition { Text = "The pleasant smell of rain on dry ground.", Example = "The petrichor after the storm." }],
                },
            ],
        };

        await using (var db = _database.CreateContext())
        {
            db.VocabularyWords.Add(new VocabularyWord { Term = "  Petrichor ", DictionaryEntry = entry });
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = _database.CreateContext())
        {
            var word = await db.VocabularyWords.Include(w => w.DictionaryEntry).SingleAsync(Ct);
            Assert.Equal("petrichor", word.NormalizedTerm);
            Assert.Equal("petrichor", word.DictionaryEntry!.NormalizedTerm);
            var meaning = Assert.Single(word.DictionaryEntry.Meanings);
            Assert.Equal("noun", meaning.PartOfSpeech);
            Assert.Equal("The petrichor after the storm.", Assert.Single(meaning.Definitions).Example);
        }
    }

    [Fact]
    public async Task Rejects_duplicate_vocabulary_words_after_normalization()
    {
        await using var db = _database.CreateContext();
        db.VocabularyWords.Add(new VocabularyWord { Term = "Sesquipedalian" });
        db.VocabularyWords.Add(new VocabularyWord { Term = "sesquipedalian " });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Rejects_out_of_range_rating(int rating)
    {
        await using var db = _database.CreateContext();
        var book = new Book { Title = "Any" };
        book.ReadingSessions.Add(new ReadingSession { Rating = rating });
        db.Books.Add(book);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Rejects_finish_before_start()
    {
        await using var db = _database.CreateContext();
        var book = new Book { Title = "Any" };
        book.ReadingSessions.Add(new ReadingSession { StartedOn = new DateOnly(2026, 2, 1), FinishedOn = new DateOnly(2026, 1, 1) });
        db.Books.Add(book);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Rejects_oversized_cover()
    {
        await using var db = _database.CreateContext();
        var book = new Book
        {
            Title = "Any",
            Cover = new BookCover { Content = new byte[BookCover.MaxSizeBytes + 1], ContentType = "image/png", Sha256 = new string('0', 64) },
        };
        db.Books.Add(book);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Deleting_a_book_cascades_sessions_and_cover_but_keeps_words()
    {
        var book = new Book
        {
            Title = "Any",
            Cover = new BookCover { Content = [1], ContentType = "image/png", Sha256 = new string('0', 64) },
        };
        book.ReadingSessions.Add(new ReadingSession());

        await using (var db = _database.CreateContext())
        {
            db.Books.Add(book);
            db.VocabularyWords.Add(new VocabularyWord { Term = "ineffable", Book = book });
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = _database.CreateContext())
        {
            await db.Books.Where(b => b.Id == book.Id).ExecuteDeleteAsync(Ct);
        }

        await using (var db = _database.CreateContext())
        {
            Assert.Empty(await db.ReadingSessions.ToListAsync(Ct));
            Assert.Empty(await db.BookCovers.ToListAsync(Ct));
            Assert.Null((await db.VocabularyWords.SingleAsync(Ct)).BookId);
        }
    }
}
