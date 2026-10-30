using Bookside.Core.Books;
using Bookside.Core.Challenge;
using Bookside.Core.Challenge.Services;
using Bookside.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace Bookside.Data.Services;

internal sealed class ChallengeService(
    IDbContextFactory<BooksideDbContext> dbFactory,
    TimeProvider timeProvider) : IChallengeService
{
    /// <summary>
    /// A year's reads aren't paged; this only guards against a runaway list.
    /// </summary>
    internal const int MaxReads = 1000;

    public async Task<Result<ChallengeYear>> GetYearAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        var validator = new InputValidator();
        CheckYear(validator, year);

        if (!validator.IsValid)
        {
            return validator.ToError();
        }

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        await using var db = await dbFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var reads = await HomeService.LoadReadsAsync(
            FinishedIn(db, year)
                .OrderByDescending(s => s.FinishedOn)
                .ThenByDescending(s => s.UpdatedAtUtc)
                .Take(MaxReads),
            cancellationToken).ConfigureAwait(false);

        var years = await ListYearsAsync(db, today, cancellationToken).ConfigureAwait(false);

        return new ChallengeYear
        {
            Year = year,
            Today = today,
            TargetBooks = years.FirstOrDefault(y => y.Year == year)?.TargetBooks,
            Reads = reads,
            Years = years,
        };
    }

    public async Task<Result> SetGoalAsync(
        int year,
        int targetBooks,
        CancellationToken cancellationToken = default)
    {
        var validator = new InputValidator();
        CheckYear(validator, year);
        validator.Check(
            targetBooks is >= ReadingGoal.MinTarget and <= ReadingGoal.MaxTarget,
            "TargetBooks",
            $"Enter a number from {ReadingGoal.MinTarget} to {ReadingGoal.MaxTarget}.");

        if (!validator.IsValid)
        {
            return validator.ToError();
        }

        try
        {
            await SaveGoalAsync(year, targetBooks, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Another call created the year's goal first; the retry updates it.
            await SaveGoalAsync(year, targetBooks, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<Result> RemoveGoalAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var deleted = await db.ReadingGoals
            .Where(g => g.Year == year)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return deleted == 0
            ? ServiceError.NotFound(
                ErrorCodes.GoalNotFound,
                $"There is no reading goal for {year}.")
            : Result.Success();
    }

    /// <summary>
    /// The year's read count and goal, shared with the home page.
    /// </summary>
    internal static async Task<ChallengeProgress> GetProgressAsync(
        BooksideDbContext db,
        int year,
        CancellationToken cancellationToken)
    {
        var readCount = await FinishedIn(db, year)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var target = await db.ReadingGoals
            .Where(g => g.Year == year)
            .Select(g => (int?)g.TargetBooks)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ChallengeProgress
        {
            Year = year,
            ReadCount = readCount,
            TargetBooks = target,
        };
    }

    /// <summary>
    /// A date range rather than <c>FinishedOn.Year</c>, so the FinishedOn
    /// index is used.
    /// </summary>
    private static IQueryable<ReadingSession> FinishedIn(BooksideDbContext db, int year)
    {
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 12, 31);

        return db.ReadingSessions.Where(s =>
            s.Status == ReadingStatus.Finished
            && s.FinishedOn >= start
            && s.FinishedOn <= end);
    }

    private static async Task<List<ChallengeProgress>> ListYearsAsync(
        BooksideDbContext db,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var counts = await db.ReadingSessions
            .Where(s => s.Status == ReadingStatus.Finished && s.FinishedOn != null)
            .GroupBy(s => s.FinishedOn!.Value.Year)
            .Select(g => new { Year = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Year, g => g.Count, cancellationToken)
            .ConfigureAwait(false);

        var goals = await db.ReadingGoals
            .ToDictionaryAsync(g => g.Year, g => g.TargetBooks, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. counts.Keys
                .Union(goals.Keys)
                .Append(today.Year)
                .Distinct()
                .OrderDescending()
                .Select(y => new ChallengeProgress
                {
                    Year = y,
                    ReadCount = counts.GetValueOrDefault(y),
                    TargetBooks = goals.TryGetValue(y, out var target) ? target : null,
                }),
        ];
    }

    private static void CheckYear(InputValidator validator, int year) =>
        validator.Check(
            year is >= ReadingGoal.MinYear and <= ReadingGoal.MaxYear,
            "Year",
            $"Enter a year from {ReadingGoal.MinYear} to {ReadingGoal.MaxYear}.");

    private async Task SaveGoalAsync(int year, int targetBooks, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var goal = await db.ReadingGoals
            .SingleOrDefaultAsync(g => g.Year == year, cancellationToken)
            .ConfigureAwait(false);

        if (goal is null)
        {
            db.ReadingGoals.Add(new ReadingGoal { Year = year, TargetBooks = targetBooks });
        }
        else
        {
            goal.TargetBooks = targetBooks;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
