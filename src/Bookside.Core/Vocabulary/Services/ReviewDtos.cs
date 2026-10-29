namespace Bookside.Core.Vocabulary.Services;

public sealed record ReviewStatus
{
    /// <summary>
    /// Reviewed words due today or earlier.
    /// </summary>
    public int DueCount { get; init; }

    /// <summary>
    /// New words that can still be started today.
    /// </summary>
    public int NewCount { get; init; }

    /// <summary>
    /// New words held back by the daily limit.
    /// </summary>
    public int NewWaitingCount { get; init; }

    /// <summary>
    /// The next date after today with words due, if any.
    /// </summary>
    public DateOnly? NextDueOn { get; init; }

    /// <summary>
    /// All words with a definition, i.e. all flash cards.
    /// </summary>
    public int ReviewableCount { get; init; }

    /// <summary>
    /// Words graded "Forgot" today.
    /// </summary>
    public int ForgottenTodayCount { get; init; }

    public int ToReviewCount => DueCount + NewCount;
}

public sealed record ReviewCard
{
    public required WordDetails Word { get; init; }

    /// <summary>
    /// Never reviewed before.
    /// </summary>
    public bool IsNew { get; init; }

    /// <summary>
    /// The interval "Knew it" would give; "Forgot" always gives
    /// <see cref="ReviewScheduler.FirstIntervalDays"/>.
    /// </summary>
    public int KnownIntervalDays { get; init; }
}

public sealed record ReviewOutcome
{
    public required DateOnly DueOn { get; init; }

    public required int IntervalDays { get; init; }
}
