using Bookside.Core.Vocabulary;

namespace Bookside.Data.Tests;

public sealed class ReviewSchedulerTests
{
    [Fact]
    public void Knowing_a_word_each_time_grows_the_interval_by_the_ease()
    {
        var state = ReviewState.New;
        var intervals = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            state = ReviewScheduler.Next(state, knew: true);
            intervals.Add(state.IntervalDays);
        }

        Assert.Equal([1, 6, 15, 38, 95], intervals);
        Assert.Equal(ReviewScheduler.InitialEase, state.Ease);
        Assert.Equal(5, state.Repetitions);
    }

    [Fact]
    public void Forgetting_restarts_at_one_day_and_lowers_the_ease()
    {
        var learnt = new ReviewState(Repetitions: 3, IntervalDays: 15, Ease: 2.5, Lapses: 0);

        var next = ReviewScheduler.Next(learnt, knew: false);

        Assert.Equal(new ReviewState(0, 1, 2.3, 1), next with { Ease = Math.Round(next.Ease, 6) });
        Assert.Equal(6, ReviewScheduler.Next(ReviewScheduler.Next(next, true), true).IntervalDays);
    }

    [Fact]
    public void Forgetting_a_new_word_is_not_a_lapse()
    {
        var next = ReviewScheduler.Next(ReviewState.New, knew: false);

        Assert.Equal(0, next.Lapses);
        Assert.Equal(1, next.IntervalDays);
    }

    [Fact]
    public void Ease_never_drops_below_the_minimum()
    {
        var state = ReviewState.New;
        for (var i = 0; i < 20; i++)
        {
            state = ReviewScheduler.Next(state, knew: false);
        }

        Assert.Equal(ReviewScheduler.MinEase, state.Ease);
    }

    [Fact]
    public void Interval_grows_by_at_least_a_day_and_is_capped()
    {
        var slow = new ReviewState(Repetitions: 2, IntervalDays: 2, Ease: 1.3, Lapses: 4);
        var long_ = new ReviewState(Repetitions: 9, IntervalDays: 3000, Ease: 2.5, Lapses: 0);

        Assert.Equal(3, ReviewScheduler.Next(slow, knew: true).IntervalDays);
        Assert.Equal(
            ReviewScheduler.MaxIntervalDays,
            ReviewScheduler.Next(long_, knew: true).IntervalDays);
    }
}
