using Bookside.Core.Common;
using Bookside.Core.Vocabulary;
using Bookside.Core.Vocabulary.Services;
using Microsoft.EntityFrameworkCore;

namespace Bookside.Data.Services;

internal sealed class ReviewService(
    IDbContextFactory<BooksideDbContext> dbFactory,
    TimeProvider timeProvider) : IReviewService
{
    internal const int MaxQueueSize = 100;

    public async Task<ReviewStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await GetStatusAsync(db, Today(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReviewCard>> GetQueueAsync(
        CancellationToken cancellationToken = default)
    {
        var today = Today();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var due = await Reviewable(db)
            .Where(w => w.Review != null && w.Review.DueOn <= today)
            .OrderBy(w => w.Review!.DueOn)
            .ThenBy(w => w.Id)
            .Take(MaxQueueSize)
            .Include(w => w.Book)
            .Include(w => w.DictionaryEntry)
            .Include(w => w.Review)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var newLimit = Math.Min(
            await NewLeftTodayAsync(db, today, cancellationToken).ConfigureAwait(false),
            MaxQueueSize - due.Count);
        var fresh = newLimit <= 0
            ? []
            : await Reviewable(db)
                .Where(w => w.Review == null)
                .OrderBy(w => w.CreatedAtUtc)
                .ThenBy(w => w.Id)
                .Take(newLimit)
                .Include(w => w.Book)
                .Include(w => w.DictionaryEntry)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        return [.. due.Concat(fresh).Select(ToCard)];
    }

    public async Task<IReadOnlyList<ReviewCard>> GetPracticeCardsAsync(
        PracticeSet words,
        CancellationToken cancellationToken = default)
    {
        var today = Today();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<VocabularyWord> query;
        Guid[]? order = null;
        switch (words)
        {
            case PracticeSet.ForgottenToday:
                query = ForgottenOn(Reviewable(db), today)
                    .OrderBy(w => w.Review!.LastReviewedOn)
                    .ThenBy(w => w.Id)
                    .Take(MaxQueueSize);
                break;
            case PracticeSet.MostForgotten:
                // Every "Forgot" lowers the ease, also for words not yet
                // learnt (which don't count as lapses).
                query = Reviewable(db)
                    .Where(w => w.Review != null
                        && (w.Review.Lapses > 0 || w.Review.Ease < ReviewScheduler.InitialEase))
                    .OrderByDescending(w => w.Review!.Lapses)
                    .ThenBy(w => w.Review!.Ease)
                    .ThenBy(w => w.Id)
                    .Take(ReviewScheduler.PracticeRoundSize);
                break;
            case PracticeSet.Random:
                var ids = await Reviewable(db)
                    .Select(w => w.Id)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);
                Random.Shared.Shuffle(ids);
                order = ids[..Math.Min(ids.Length, ReviewScheduler.PracticeRoundSize)];
                query = Reviewable(db).Where(w => order.Contains(w.Id));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(words), words, null);
        }

        var cards = await query
            .Include(w => w.Book)
            .Include(w => w.DictionaryEntry)
            .Include(w => w.Review)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (order is not null)
        {
            cards = [.. cards.OrderBy(w => Array.IndexOf(order, w.Id))];
        }

        return [.. cards.Select(ToCard)];
    }

    public async Task<Result<ReviewOutcome>> RecordReviewAsync(
        Guid wordId,
        bool knew,
        CancellationToken cancellationToken = default)
    {
        var today = Today();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var word = await db.VocabularyWords
            .Include(w => w.Review)
            .SingleOrDefaultAsync(w => w.Id == wordId, cancellationToken)
            .ConfigureAwait(false);
        if (word is null)
        {
            return ServiceError.NotFound(ErrorCodes.WordNotFound, $"Word {wordId} was not found.");
        }

        if (word.Review is { } reviewed && reviewed.LastReviewedOn >= today)
        {
            return ToOutcome(reviewed);
        }

        var review = word.Review ??= new WordReview { WordId = word.Id, FirstReviewedOn = today };
        var next = ReviewScheduler.Next(ToState(review), knew);
        review.Repetitions = next.Repetitions;
        review.IntervalDays = next.IntervalDays;
        review.Ease = next.Ease;
        review.Lapses = next.Lapses;
        review.LastReviewedOn = today;
        review.DueOn = today.AddDays(next.IntervalDays);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Lost a race to create the first review (e.g. a double click): that one counts.
            var existing = await db.WordReviews.AsNoTracking()
                .SingleOrDefaultAsync(r => r.WordId == wordId, cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                throw;
            }

            return ToOutcome(existing);
        }

        return ToOutcome(review);
    }

    /// <summary>
    /// Shared with the home page summary.
    /// </summary>
    internal static async Task<ReviewStatus> GetStatusAsync(
        BooksideDbContext db,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var dueCount = await Reviewable(db)
            .CountAsync(w => w.Review != null && w.Review.DueOn <= today, cancellationToken)
            .ConfigureAwait(false);
        var newTotal = await Reviewable(db)
            .CountAsync(w => w.Review == null, cancellationToken)
            .ConfigureAwait(false);
        var newCount = Math.Min(
            newTotal,
            await NewLeftTodayAsync(db, today, cancellationToken).ConfigureAwait(false));
        var nextDueOn = await Reviewable(db)
            .Where(w => w.Review != null && w.Review.DueOn > today)
            .Select(w => (DateOnly?)w.Review!.DueOn)
            .MinAsync(cancellationToken)
            .ConfigureAwait(false);
        var reviewableCount = await Reviewable(db)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
        var forgottenTodayCount = await ForgottenOn(Reviewable(db), today)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ReviewStatus
        {
            DueCount = dueCount,
            NewCount = newCount,
            NewWaitingCount = newTotal - newCount,
            NextDueOn = nextDueOn,
            ReviewableCount = reviewableCount,
            ForgottenTodayCount = forgottenTodayCount,
        };
    }

    /// <summary>
    /// Words with a definition to show on the back of the card.
    /// </summary>
    private static IQueryable<VocabularyWord> Reviewable(BooksideDbContext db) =>
        db.VocabularyWords.AsNoTracking()
            .Where(w => w.DictionaryEntry != null
                && w.DictionaryEntry.Status == DictionaryLookupStatus.Found);

    /// <summary>
    /// Words graded "Forgot" on <paramref name="day"/>: "Knew it" always
    /// leaves at least one repetition.
    /// </summary>
    private static IQueryable<VocabularyWord> ForgottenOn(
        IQueryable<VocabularyWord> words,
        DateOnly day) =>
            words.Where(w => w.Review != null
                && w.Review.LastReviewedOn == day
                && w.Review.Repetitions == 0);

    private static async Task<int> NewLeftTodayAsync(
        BooksideDbContext db,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var startedToday = await db.WordReviews
            .CountAsync(r => r.FirstReviewedOn == today, cancellationToken)
            .ConfigureAwait(false);
        return Math.Max(0, ReviewScheduler.NewWordsPerDay - startedToday);
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private static ReviewCard ToCard(VocabularyWord word)
    {
        var state = word.Review is { } review ? ToState(review) : ReviewState.New;
        return new ReviewCard
        {
            Word = VocabularyService.ToDetails(word),
            IsNew = word.Review is null,
            KnownIntervalDays = ReviewScheduler.Next(state, knew: true).IntervalDays,
        };
    }

    private static ReviewState ToState(WordReview review) =>
        new(review.Repetitions, review.IntervalDays, review.Ease, review.Lapses);

    private static ReviewOutcome ToOutcome(WordReview review) =>
        new() { DueOn = review.DueOn, IntervalDays = review.IntervalDays };
}
