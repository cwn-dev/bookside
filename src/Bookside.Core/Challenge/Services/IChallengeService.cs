using Bookside.Core.Common;

namespace Bookside.Core.Challenge.Services;

/// <summary>
/// Yearly reading goals. A year's count is its finished reading sessions with
/// a finish date in that year; re-reads count again.
/// </summary>
public interface IChallengeService
{
    /// <summary>
    /// The year's goal, finished reads and the list of years, in one call.
    /// Validation error for a year outside
    /// <see cref="ReadingGoal.MinYear"/>–<see cref="ReadingGoal.MaxYear"/>.
    /// </summary>
    Task<Result<ChallengeYear>> GetYearAsync(
        int year,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or replaces the year's goal. Field errors are on "Year" and
    /// "TargetBooks".
    /// </summary>
    Task<Result> SetGoalAsync(
        int year,
        int targetBooks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// NotFound when the year has no goal.
    /// </summary>
    Task<Result> RemoveGoalAsync(
        int year,
        CancellationToken cancellationToken = default);
}
