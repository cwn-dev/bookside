using Bookside.Core.Activity.Services;
using Bookside.Core.Books;
using Bookside.Core.Challenge;
using Bookside.Core.Common;
using Bookside.Core.Search;
using Microsoft.EntityFrameworkCore;

namespace Bookside.Data.Services;

/// <summary>
/// One context, one query per kind of activity. Created-at timestamps are
/// placed on the server's local date.
/// </summary>
internal sealed class ActivityService(
    IDbContextFactory<BooksideDbContext> dbFactory,
    TimeProvider timeProvider) : IActivityService
{
    // shortcut: a per-kind cap rather than paging; page if a month exceeds it.
    internal const int MaxPerKind = 500;

    internal const int CommentLength = 80;

    public async Task<Result<ActivityMonth>> GetMonthAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var validator = new InputValidator();
        validator.Check(
            year is >= ReadingGoal.MinYear and <= ReadingGoal.MaxYear,
            "Year",
            $"Enter a year from {ReadingGoal.MinYear} to {ReadingGoal.MaxYear}.");
        validator.Check(month is >= 1 and <= 12, "Month", "Enter a month from 1 to 12.");

        if (!validator.IsValid)
        {
            return validator.ToError();
        }

        var zone = timeProvider.LocalTimeZone;
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        // The calendar shows whole Monday-to-Sunday weeks, so load the days of
        // the months either side that share the month's first and last weeks.
        var monthFirst = new DateOnly(year, month, 1);
        var monthLast = monthFirst.AddDays(DateTime.DaysInMonth(year, month) - 1);
        var first = monthFirst.AddDays(-DaysFromMonday(monthFirst));
        var last = DateOnly.FromDayNumber(Math.Min(
            monthLast.DayNumber + 6 - DaysFromMonday(monthLast),
            DateOnly.MaxValue.DayNumber));
        var fromUtc = HomeService.LocalMidnightToUtc(
            first.ToDateTime(TimeOnly.MinValue),
            zone);
        var toUtc = last == DateOnly.MaxValue
            ? DateTime.MaxValue
            : HomeService.LocalMidnightToUtc(
                last.AddDays(1).ToDateTime(TimeOnly.MinValue),
                zone);

        await using var db = await dbFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var reads = await LoadReadsAsync(db, first, last, today, cancellationToken)
            .ConfigureAwait(false);

        var events = new List<ActivityEvent>();

        var books = await db.Books.AsNoTracking()
            .Where(b => b.CreatedAtUtc >= fromUtc && b.CreatedAtUtc < toUtc)
            .OrderBy(b => b.CreatedAtUtc)
            .Take(MaxPerKind)
            .Select(b => new { b.Id, b.Title, b.CreatedAtUtc })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        events.AddRange(books.Select(b => new ActivityEvent
        {
            Date = LocalDate(b.CreatedAtUtc, zone),
            Kind = ActivityKind.BookAdded,
            BookId = b.Id,
            BookTitle = b.Title,
        }));

        var words = await db.VocabularyWords.AsNoTracking()
            .Where(w => w.CreatedAtUtc >= fromUtc && w.CreatedAtUtc < toUtc)
            .OrderBy(w => w.CreatedAtUtc)
            .Take(MaxPerKind)
            .Select(w => new
            {
                w.Id,
                w.Term,
                w.BookId,
                BookTitle = w.Book == null ? null : w.Book.Title,
                w.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        events.AddRange(words.Select(w => new ActivityEvent
        {
            Date = LocalDate(w.CreatedAtUtc, zone),
            Kind = ActivityKind.WordAdded,
            WordId = w.Id,
            Text = w.Term,
            BookId = w.BookId,
            BookTitle = w.BookTitle,
        }));

        var comments = await db.BookComments.AsNoTracking()
            .Where(c => c.CreatedAtUtc >= fromUtc && c.CreatedAtUtc < toUtc)
            .OrderBy(c => c.CreatedAtUtc)
            .Take(MaxPerKind)
            .Select(c => new { c.BookId, BookTitle = c.Book.Title, c.Text, c.CreatedAtUtc })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        events.AddRange(comments.Select(c => new ActivityEvent
        {
            Date = LocalDate(c.CreatedAtUtc, zone),
            Kind = ActivityKind.Comment,
            BookId = c.BookId,
            BookTitle = c.BookTitle,
            Text = SearchSnippet.Around(c.Text, string.Empty, CommentLength),
        }));

        var flashCards = await db.WordReviews.AsNoTracking()
            .Where(r => r.FirstReviewedOn >= first && r.FirstReviewedOn <= last)
            .GroupBy(r => r.FirstReviewedOn)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        events.AddRange(flashCards.Select(f => new ActivityEvent
        {
            Date = f.Date,
            Kind = ActivityKind.FlashCardsStarted,
            Count = f.Count,
        }));

        // The first and last weeks can reach into the years either side.
        foreach (var goalYear in new[] { first.Year, last.Year }.Distinct())
        {
            if (await GoalReachedOnAsync(db, goalYear, cancellationToken).ConfigureAwait(false)
                is ({ } reachedOn, var target)
                && reachedOn >= first
                && reachedOn <= last)
            {
                events.Add(new ActivityEvent
                {
                    Date = reachedOn,
                    Kind = ActivityKind.GoalReached,
                    Count = target,
                });
            }
        }

        return new ActivityMonth
        {
            Year = year,
            Month = month,
            Today = today,
            Reads = reads,
            Events = [.. events.OrderBy(e => e.Date)],
        };
    }

    internal static async Task<List<ActivityRead>> LoadReadsAsync(
        BooksideDbContext db,
        DateOnly first,
        DateOnly last,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        // A span overlaps the month when it starts by its end and ends after
        // its start. No index helps an overlap test; sessions are few.
        var rows = await db.ReadingSessions.AsNoTracking()
            .Where(s => (s.StartedOn ?? s.FinishedOn) <= last
                && (s.FinishedOn
                    ?? (s.Status == ReadingStatus.Reading ? today : s.StartedOn)) >= first)
            .OrderBy(s => s.StartedOn ?? s.FinishedOn)
            .ThenBy(s => s.Id)
            .Take(MaxPerKind)
            .Select(s => new
            {
                s.Id,
                s.BookId,
                s.Book.Title,
                s.StartedOn,
                s.FinishedOn,
                s.Status,
                s.Rating,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(s =>
            {
                var from = (s.StartedOn ?? s.FinishedOn)!.Value;
                var to = s.FinishedOn
                    ?? (s.Status == ReadingStatus.Reading ? today : from);

                return new ActivityRead
                {
                    SessionId = s.Id,
                    BookId = s.BookId,
                    Title = s.Title,
                    From = from,
                    // A read started in the future hasn't run any days yet.
                    To = to < from ? from : to,
                    StartedOn = s.StartedOn,
                    FinishedOn = s.FinishedOn,
                    Status = s.Status,
                    Rating = s.Rating,
                };
            }),
        ];
    }

    /// <summary>
    /// Activity points per local day from <paramref name="first"/> to
    /// <paramref name="last"/>, non-zero days only: one per book being read,
    /// book added, word added and comment, and one for a flash-card day.
    /// </summary>
    internal static async Task<Dictionary<DateOnly, int>> CountDaysAsync(
        BooksideDbContext db,
        DateOnly first,
        DateOnly last,
        DateOnly today,
        TimeZoneInfo zone,
        CancellationToken cancellationToken)
    {
        var fromUtc = HomeService.LocalMidnightToUtc(
            first.ToDateTime(TimeOnly.MinValue),
            zone);
        var toUtc = HomeService.LocalMidnightToUtc(
            last.AddDays(1).ToDateTime(TimeOnly.MinValue),
            zone);
        var counts = new Dictionary<DateOnly, int>();

        void Add(DateOnly date)
        {
            if (date >= first && date <= last)
            {
                counts[date] = counts.GetValueOrDefault(date) + 1;
            }
        }

        foreach (var read in await LoadReadsAsync(db, first, last, today, cancellationToken)
            .ConfigureAwait(false))
        {
            for (var day = read.From; day <= read.To; day = day.AddDays(1))
            {
                Add(day);
            }
        }

        var created = new List<DateTime>();
        created.AddRange(await db.Books
            .Where(b => b.CreatedAtUtc >= fromUtc && b.CreatedAtUtc < toUtc)
            .Select(b => b.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));
        created.AddRange(await db.VocabularyWords
            .Where(w => w.CreatedAtUtc >= fromUtc && w.CreatedAtUtc < toUtc)
            .Select(w => w.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));
        created.AddRange(await db.BookComments
            .Where(c => c.CreatedAtUtc >= fromUtc && c.CreatedAtUtc < toUtc)
            .Select(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));
        foreach (var utc in created)
        {
            Add(LocalDate(utc, zone));
        }

        var flashCardDays = await db.WordReviews
            .Where(r => r.FirstReviewedOn >= first && r.FirstReviewedOn <= last)
            .Select(r => r.FirstReviewedOn)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var day in flashCardDays)
        {
            Add(day);
        }

        return counts;
    }

    /// <summary>
    /// The finish date of the read that took the year's count to its goal.
    /// </summary>
    private static async Task<(DateOnly? On, int Target)> GoalReachedOnAsync(
        BooksideDbContext db,
        int year,
        CancellationToken cancellationToken)
    {
        var target = await db.ReadingGoals
            .Where(g => g.Year == year)
            .Select(g => (int?)g.TargetBooks)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (target is not int books)
        {
            return (null, 0);
        }

        var on = await ChallengeService.FinishedIn(db, year)
            .OrderBy(s => s.FinishedOn)
            .Skip(books - 1)
            .Select(s => s.FinishedOn)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return (on, books);
    }

    private static int DaysFromMonday(DateOnly date) => ((int)date.DayOfWeek + 6) % 7;

    private static DateOnly LocalDate(DateTime utc, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, zone));
}
