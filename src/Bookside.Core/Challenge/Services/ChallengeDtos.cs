using Bookside.Core.Home.Services;

namespace Bookside.Core.Challenge.Services;

/// <summary>
/// Finished reads in a year and the year's goal, if any.
/// </summary>
public sealed record ChallengeProgress
{
    public required int Year { get; init; }

    /// <summary>
    /// Finished reading sessions dated in the year; re-reads count again.
    /// </summary>
    public required int ReadCount { get; init; }

    public int? TargetBooks { get; init; }
}

/// <summary>
/// One year's challenge page.
/// </summary>
public sealed record ChallengeYear
{
    public required int Year { get; init; }

    /// <summary>
    /// The server's local date, for pace (set TZ in the container).
    /// </summary>
    public required DateOnly Today { get; init; }

    public int? TargetBooks { get; init; }

    /// <summary>
    /// The year's finished reads, latest finish first. A book read twice is
    /// listed twice.
    /// </summary>
    public required IReadOnlyList<BookRead> Reads { get; init; }

    /// <summary>
    /// Every year with finished reads or a goal, plus the current year,
    /// newest first.
    /// </summary>
    public required IReadOnlyList<ChallengeProgress> Years { get; init; }
}
