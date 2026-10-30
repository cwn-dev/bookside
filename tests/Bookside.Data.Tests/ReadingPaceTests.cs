using Bookside.Core.Challenge;

namespace Bookside.Data.Tests;

public sealed class ReadingPaceTests
{
    [Fact]
    public void Halfway_through_the_year_half_the_goal_is_on_track()
    {
        // Day 182 of 365: 49.9 % of the year gone, 24.9 books expected.
        var pace = ReadingPace.For(2026, 24, 50, new DateOnly(2026, 7, 1));

        Assert.Equal(ChallengeState.InProgress, pace.State);
        Assert.Equal(0.48, pace.Fraction);
        Assert.Equal(0, pace.Ahead);
        Assert.Equal(26, pace.Remaining);
        Assert.Equal(26 * 7.0 / 184, pace.PerWeekNeeded);
    }

    [Fact]
    public void Reading_more_or_less_than_an_even_pace_is_ahead_or_behind()
    {
        var today = new DateOnly(2026, 10, 30); // 303 / 365 → 41.5 of 50 expected

        Assert.Equal(3, ReadingPace.For(2026, 44, 50, today).Ahead);
        Assert.Equal(-2, ReadingPace.For(2026, 39, 50, today).Ahead);
    }

    [Fact]
    public void The_first_day_expects_a_share_of_one_day()
    {
        var pace = ReadingPace.For(2026, 0, 365, new DateOnly(2026, 1, 1));

        Assert.Equal(1.0 / 365, pace.ExpectedFraction);
        Assert.Equal(-1, pace.Ahead);
        Assert.Equal(7, pace.PerWeekNeeded);
    }

    [Fact]
    public void Leap_years_have_366_days()
    {
        var pace = ReadingPace.For(2028, 0, 1, new DateOnly(2028, 12, 31));

        Assert.Equal(1.0, pace.ExpectedFraction);
        Assert.Equal(-1, pace.Ahead);
        Assert.Equal(7, pace.PerWeekNeeded);
    }

    [Fact]
    public void Reaching_the_goal_fills_the_bar_and_stops_the_pace()
    {
        var pace = ReadingPace.For(2026, 55, 50, new DateOnly(2026, 6, 1));

        Assert.Equal(ChallengeState.Reached, pace.State);
        Assert.Equal(1.0, pace.Fraction);
        Assert.Equal(0, pace.Remaining);
        Assert.Null(pace.PerWeekNeeded);
        Assert.True(pace.Ahead > 0);
    }

    [Fact]
    public void Past_years_end_reached_or_not_without_a_pace()
    {
        var today = new DateOnly(2026, 6, 1);

        var missed = ReadingPace.For(2025, 10, 20, today);
        var reached = ReadingPace.For(2025, 1, 1, today);

        Assert.Equal(ChallengeState.Ended, missed.State);
        Assert.Equal(0.5, missed.Fraction);
        Assert.Null(missed.ExpectedFraction);
        Assert.Null(missed.PerWeekNeeded);
        Assert.Equal(0, missed.Ahead);
        Assert.Equal(ChallengeState.Reached, reached.State);
    }

    [Fact]
    public void Future_years_have_not_started()
    {
        var pace = ReadingPace.For(2027, 0, 30, new DateOnly(2026, 6, 1));

        Assert.Equal(ChallengeState.Future, pace.State);
        Assert.Null(pace.ExpectedFraction);
        Assert.Null(pace.PerWeekNeeded);
    }
}
