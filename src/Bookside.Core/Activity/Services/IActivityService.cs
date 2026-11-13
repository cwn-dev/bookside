using Bookside.Core.Common;

namespace Bookside.Core.Activity.Services;

/// <summary>
/// Everything dated in a month, for the activity calendar.
/// </summary>
public interface IActivityService
{
    /// <summary>
    /// The month's reads (every session whose span overlaps it) and events,
    /// in one call. Validation error for a year outside
    /// <see cref="Challenge.ReadingGoal.MinYear"/>–<see cref="Challenge.ReadingGoal.MaxYear"/>
    /// or a month outside 1–12.
    /// </summary>
    Task<Result<ActivityMonth>> GetMonthAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);
}
