using Bookside.Core.Books;
using Bookside.Core.Home.Services;
using Microsoft.EntityFrameworkCore;

namespace Bookside.Data.Services;

internal sealed class HomeService(IDbContextFactory<BooksideDbContext> dbFactory, TimeProvider timeProvider) : IHomeService
{
    internal const int MaxReading = 6;
    internal const int RecentlyFinishedCount = 4;
    internal const int RecentWordsCount = 5;

    public async Task<HomeSummary> GetHomeAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var reading = await LoadReadsAsync(
            db.ReadingSessions
                .Where(s => s.Status == ReadingStatus.Reading)
                .OrderByDescending(s => s.StartedOn)
                .ThenByDescending(s => s.CreatedAtUtc)
                .Take(MaxReading),
            cancellationToken).ConfigureAwait(false);

        // Latest finish per book. Over-fetch and de-duplicate here: simpler than a per-group query, and a book
        // re-read many times in a row is the only case where it could show fewer than RecentlyFinishedCount.
        var finished = await LoadReadsAsync(
            db.ReadingSessions
                .Where(s => s.Status == ReadingStatus.Finished)
                .OrderByDescending(s => s.FinishedOn)
                .ThenByDescending(s => s.UpdatedAtUtc)
                .Take(RecentlyFinishedCount * 5),
            cancellationToken).ConfigureAwait(false);

        var recentWords = await db.VocabularyWords.AsNoTracking()
            .OrderByDescending(w => w.CreatedAtUtc)
            .ThenByDescending(w => w.Id)
            .Take(RecentWordsCount)
            .Include(w => w.Book)
            .Include(w => w.DictionaryEntry)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var wordOfTheDay = await VocabularyService
            .PickDefinedWordAsync(db, count => DayIndex(today, count), excludeWordId: null, cancellationToken)
            .ConfigureAwait(false);

        return new HomeSummary
        {
            Reading = reading,
            RecentlyFinished = [.. finished.DistinctBy(r => r.BookId).Take(RecentlyFinishedCount)],
            RecentWords = [.. recentWords.Select(VocabularyService.ToSummary)],
            WordOfTheDay = wordOfTheDay,
            Stats = await LoadStatsAsync(db, today, cancellationToken).ConfigureAwait(false),
        };
    }

    private static async Task<List<BookRead>> LoadReadsAsync(IQueryable<ReadingSession> sessions, CancellationToken cancellationToken)
    {
        var rows = await sessions.AsNoTracking()
            .Select(s => new
            {
                Session = s,
                s.Book.Title,
                Authors = s.Book.Authors.OrderBy(a => a.Position).Select(a => a.Author.Name).ToList(),
                CoverSha256 = s.Book.Cover!.Sha256,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(r => new BookRead
            {
                BookId = r.Session.BookId,
                Title = r.Title,
                Authors = r.Authors,
                CoverSha256 = r.CoverSha256,
                Session = LibraryService.ToDto(r.Session),
            }),
        ];
    }

    private async Task<HomeStats> LoadStatsAsync(BooksideDbContext db, DateOnly today, CancellationToken cancellationToken)
    {
        var yearStart = new DateOnly(today.Year, 1, 1);
        var yearEnd = new DateOnly(today.Year, 12, 31);
        var monthStartUtc = LocalMidnightToUtc(new DateTime(today.Year, today.Month, 1), timeProvider.LocalTimeZone);

        return new HomeStats
        {
            Today = today,
            BookCount = await db.Books.CountAsync(cancellationToken).ConfigureAwait(false),
            WordCount = await db.VocabularyWords.CountAsync(cancellationToken).ConfigureAwait(false),
            BooksFinishedThisYear = await db.ReadingSessions
                .Where(s => s.Status == ReadingStatus.Finished && s.FinishedOn >= yearStart && s.FinishedOn <= yearEnd)
                .Select(s => s.BookId)
                .Distinct()
                .CountAsync(cancellationToken)
                .ConfigureAwait(false),
            WordsAddedThisMonth = await db.VocabularyWords
                .CountAsync(w => w.CreatedAtUtc >= monthStartUtc, cancellationToken)
                .ConfigureAwait(false),
        };
    }

    /// <summary>Where a DST change skips midnight, the day starts at the first valid local time.</summary>
    private static DateTime LocalMidnightToUtc(DateTime localMidnight, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(localMidnight, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(15);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    /// <summary>A stable, well-spread index for the date (SplitMix64), so consecutive days don't walk the list in order.</summary>
    internal static int DayIndex(DateOnly date, int count)
    {
        var x = (ulong)date.DayNumber + 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x % (ulong)count);
    }
}
