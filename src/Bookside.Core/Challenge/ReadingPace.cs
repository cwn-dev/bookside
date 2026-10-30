namespace Bookside.Core.Challenge;

public enum ChallengeState
{
    /// <summary>
    /// The year hasn't started.
    /// </summary>
    Future = 0,

    /// <summary>
    /// The current year, goal not reached yet.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// At least as many books read as the goal (any year).
    /// </summary>
    Reached = 2,

    /// <summary>
    /// A past year whose goal wasn't reached.
    /// </summary>
    Ended = 3,
}

/// <summary>
/// Progress towards a yearly goal, and for the current year whether the
/// reader is ahead of or behind an even pace.
/// </summary>
public sealed record ReadingPace
{
    public required int Year { get; init; }

    public required int ReadCount { get; init; }

    public required int TargetBooks { get; init; }

    public required ChallengeState State { get; init; }

    /// <summary>
    /// Read count over target, 0–1 (capped once the goal is passed).
    /// </summary>
    public required double Fraction { get; init; }

    /// <summary>
    /// Share of the year gone by the end of today, 0–1; null outside the
    /// current year.
    /// </summary>
    public double? ExpectedFraction { get; init; }

    /// <summary>
    /// Books ahead (positive) or behind (negative) of an even pace by the end
    /// of today; 0 is on track, and always 0 outside the current year.
    /// </summary>
    public int Ahead { get; init; }

    /// <summary>
    /// Books still to read to reach the goal; 0 once reached.
    /// </summary>
    public int Remaining => Math.Max(0, TargetBooks - ReadCount);

    /// <summary>
    /// Books a week needed for the rest of the year (today included) to reach
    /// the goal; null unless the challenge is in progress.
    /// </summary>
    public double? PerWeekNeeded { get; init; }

    public static ReadingPace For(int year, int readCount, int targetBooks, DateOnly today)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(targetBooks, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(readCount);

        var state = year > today.Year ? ChallengeState.Future
            : readCount >= targetBooks ? ChallengeState.Reached
            : year < today.Year ? ChallengeState.Ended
            : ChallengeState.InProgress;

        var pace = new ReadingPace
        {
            Year = year,
            ReadCount = readCount,
            TargetBooks = targetBooks,
            State = state,
            Fraction = Math.Min(1.0, (double)readCount / targetBooks),
        };

        if (year != today.Year)
        {
            return pace;
        }

        var daysInYear = DateTime.IsLeapYear(year) ? 366 : 365;
        var elapsed = (double)today.DayOfYear / daysInYear;
        var daysLeft = daysInYear - today.DayOfYear + 1;

        return pace with
        {
            ExpectedFraction = elapsed,
            Ahead = readCount - (int)Math.Floor(targetBooks * elapsed),
            PerWeekNeeded = state == ChallengeState.InProgress
                ? (targetBooks - readCount) * 7.0 / daysLeft
                : null,
        };
    }
}
