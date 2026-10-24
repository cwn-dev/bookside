using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.Core.Home.Services;
using Bookside.Core.Vocabulary.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.Data.Tests.Services;

public sealed class HomeServiceTests : ServiceTestBase
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    private readonly TestClock _clock = new(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureServices(IServiceCollection services) => services.AddSingleton<TimeProvider>(_clock);

    [Fact]
    public async Task An_empty_library_has_nothing_to_show()
    {
        var home = await Home.GetHomeAsync(Ct);

        Assert.Empty(home.Reading);
        Assert.Empty(home.RecentlyFinished);
        Assert.Empty(home.RecentWords);
        Assert.Null(home.WordOfTheDay);
        Assert.Equal(new HomeStats { Today = Today }, home.Stats);
    }

    [Fact]
    public async Task Reading_lists_books_in_progress_most_recently_started_first()
    {
        var older = await AddBookAsync("Mort", "Terry Pratchett");
        var newer = await AddBookAsync("Good Omens", "Terry Pratchett", "Neil Gaiman");
        var notReading = await AddBookAsync("Sourcery");
        await AddSessionAsync(older.Id, ReadingStatus.Reading, started: new(2026, 6, 1));
        await AddSessionAsync(newer.Id, ReadingStatus.Reading, started: new(2026, 6, 10));
        await AddSessionAsync(notReading.Id, ReadingStatus.Abandoned, started: new(2026, 6, 12), finished: new(2026, 6, 13));

        var home = await Home.GetHomeAsync(Ct);

        Assert.Equal(["Good Omens", "Mort"], home.Reading.Select(r => r.Title));
        var first = home.Reading[0];
        Assert.Equal(newer.Id, first.BookId);
        Assert.Equal(["Terry Pratchett", "Neil Gaiman"], first.Authors);
        Assert.Equal(new DateOnly(2026, 6, 10), first.Session.StartedOn);
        Assert.Equal(ReadingStatus.Reading, first.Session.Status);
    }

    [Fact]
    public async Task Recently_finished_shows_each_books_latest_finish_newest_first()
    {
        var reread = await AddBookAsync("Mort");
        await AddSessionAsync(reread.Id, ReadingStatus.Finished, finished: new(2025, 3, 1), rating: 3);
        await AddSessionAsync(reread.Id, ReadingStatus.Finished, finished: new(2026, 5, 1), rating: 5);
        foreach (var (title, day) in new[] { ("A", 2), ("B", 3), ("C", 4), ("D", 5) })
        {
            var book = await AddBookAsync(title);
            await AddSessionAsync(book.Id, ReadingStatus.Finished, finished: new(2026, 4, day));
        }

        var abandoned = await AddBookAsync("Abandoned");
        await AddSessionAsync(abandoned.Id, ReadingStatus.Abandoned, started: new(2026, 6, 1), finished: new(2026, 6, 2));

        var home = await Home.GetHomeAsync(Ct);

        Assert.Equal(["Mort", "D", "C", "B"], home.RecentlyFinished.Select(r => r.Title));
        Assert.Equal(5, home.RecentlyFinished[0].Session.Rating);
    }

    [Fact]
    public async Task Recent_words_are_the_latest_added()
    {
        Dictionary.Define("ennui", "Listlessness.");
        foreach (var term in new[] { "a1", "a2", "a3", "ennui", "a5", "a6" })
        {
            Success(await Vocabulary.AddWordAsync(new WordInput { Term = term }, Ct));
        }

        var home = await Home.GetHomeAsync(Ct);

        Assert.Equal(["a6", "a5", "ennui", "a3", "a2"], home.RecentWords.Select(w => w.Term));
        Assert.Equal("Listlessness.", home.RecentWords[2].ShortDefinition);
    }

    [Fact]
    public async Task Stats_count_this_years_finished_books_and_this_months_words()
    {
        var book = await AddBookAsync("Mort");
        await AddSessionAsync(book.Id, ReadingStatus.Finished, finished: new(2026, 1, 1));
        await AddSessionAsync(book.Id, ReadingStatus.Finished, finished: new(2026, 5, 1));
        var lastYear = await AddBookAsync("Sourcery");
        await AddSessionAsync(lastYear.Id, ReadingStatus.Finished, finished: new(2025, 12, 31));
        var stopped = await AddBookAsync("Eric");
        await AddSessionAsync(stopped.Id, ReadingStatus.Abandoned, finished: new(2026, 2, 1));

        _clock.Set(new DateTimeOffset(2026, 5, 31, 23, 59, 0, TimeSpan.Zero));
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "may" }, Ct));
        _clock.Set(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "june" }, Ct));
        _clock.Set(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));

        var stats = (await Home.GetHomeAsync(Ct)).Stats;

        Assert.Equal(new HomeStats
        {
            Today = Today,
            BookCount = 3,
            WordCount = 2,
            BooksFinishedThisYear = 1,
            WordsAddedThisMonth = 1,
        }, stats);
    }

    [Fact]
    public async Task Stats_months_start_at_local_midnight()
    {
        // UTC+10: 2026-06-01 00:00 local is 2026-05-31 14:00 UTC.
        _clock.Zone = TimeZoneInfo.CreateCustomTimeZone("Test+10", TimeSpan.FromHours(10), "Test+10", "Test+10");
        _clock.Set(new DateTimeOffset(2026, 5, 31, 13, 59, 0, TimeSpan.Zero));
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "may" }, Ct));
        _clock.Set(new DateTimeOffset(2026, 5, 31, 14, 0, 0, TimeSpan.Zero));
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "june" }, Ct));

        var stats = (await Home.GetHomeAsync(Ct)).Stats;

        Assert.Equal(new DateOnly(2026, 6, 1), stats.Today);
        Assert.Equal(1, stats.WordsAddedThisMonth);
    }

    [Fact]
    public async Task Word_of_the_day_is_a_defined_word_and_stays_the_same_all_day()
    {
        foreach (var term in new[] { "ennui", "petrichor", "sonder" })
        {
            Dictionary.Define(term, $"Meaning of {term}.");
            Success(await Vocabulary.AddWordAsync(new WordInput { Term = term }, Ct));
        }

        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "undefined" }, Ct));

        var morning = (await Home.GetHomeAsync(Ct)).WordOfTheDay;
        _clock.Set(new DateTimeOffset(2026, 6, 15, 23, 0, 0, TimeSpan.Zero));
        var evening = (await Home.GetHomeAsync(Ct)).WordOfTheDay;

        Assert.NotNull(morning);
        Assert.NotEqual("undefined", morning.Term);
        Assert.Equal(morning.Id, evening?.Id);
    }

    [Fact]
    public async Task Word_of_the_day_is_null_without_defined_words()
    {
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "undefined" }, Ct));

        Assert.Null((await Home.GetHomeAsync(Ct)).WordOfTheDay);
    }

    [Fact]
    public async Task Word_of_the_day_changes_across_days()
    {
        foreach (var term in new[] { "a", "b", "c", "d", "e" })
        {
            Dictionary.Define(term, $"Meaning of {term}.");
            Success(await Vocabulary.AddWordAsync(new WordInput { Term = term }, Ct));
        }

        var picks = new HashSet<string>();
        for (var day = 1; day <= 30; day++)
        {
            _clock.Set(new DateTimeOffset(2026, 6, day, 12, 0, 0, TimeSpan.Zero));
            picks.Add((await Home.GetHomeAsync(Ct)).WordOfTheDay!.Term);
        }

        Assert.True(picks.Count >= 4, $"Only {picks.Count} distinct words in 30 days.");
    }

    private async Task AddSessionAsync(
        Guid bookId,
        ReadingStatus status,
        DateOnly? started = null,
        DateOnly? finished = null,
        int? rating = null) =>
        Success(await Library.AddReadingSessionAsync(
            bookId,
            new ReadingSessionInput { Status = status, StartedOn = started, FinishedOn = finished, Rating = rating },
            Ct));
}
