namespace Bookside.Core.Vocabulary;

/// <summary>
/// SM-2 spaced repetition with two grades. "Knew it" is SM-2 quality 4, which
/// leaves the ease unchanged; "Forgot" restarts the word at one day and lowers
/// the ease by <see cref="LapseEasePenalty"/> (Anki's penalty, gentler than
/// SM-2's own).
/// </summary>
public static class ReviewScheduler
{
    public const double InitialEase = 2.5;
    public const double MinEase = 1.3;
    public const double LapseEasePenalty = 0.2;
    public const int FirstIntervalDays = 1;
    public const int SecondIntervalDays = 6;
    public const int MaxIntervalDays = 3650;

    /// <summary>
    /// New words that can be started per day, so a backlog doesn't arrive all
    /// at once.
    /// </summary>
    public const int NewWordsPerDay = 10;

    /// <summary>
    /// The state after one review graded <paramref name="knew"/>.
    /// </summary>
    public static ReviewState Next(ReviewState state, bool knew)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!knew)
        {
            return state with
            {
                Repetitions = 0,
                IntervalDays = FirstIntervalDays,
                Ease = Math.Max(MinEase, state.Ease - LapseEasePenalty),
                Lapses = state.Repetitions > 0 ? state.Lapses + 1 : state.Lapses,
            };
        }

        var interval = state.Repetitions switch
        {
            0 => FirstIntervalDays,
            1 => SecondIntervalDays,
            _ => Math.Max(
                state.IntervalDays + 1,
                (int)Math.Round(state.IntervalDays * state.Ease)),
        };

        return state with
        {
            Repetitions = state.Repetitions + 1,
            IntervalDays = Math.Min(interval, MaxIntervalDays),
        };
    }
}

/// <summary>
/// The scheduling part of a <see cref="WordReview"/>.
/// </summary>
public sealed record ReviewState(int Repetitions, int IntervalDays, double Ease, int Lapses)
{
    /// <summary>
    /// A word never reviewed.
    /// </summary>
    public static ReviewState New { get; } = new(0, 0, ReviewScheduler.InitialEase, 0);
}
