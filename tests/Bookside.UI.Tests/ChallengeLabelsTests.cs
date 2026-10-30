using Bookside.Core.Challenge;

namespace Bookside.UI.Tests;

public sealed class ChallengeLabelsTests
{
    private static readonly DateOnly Today = new(2026, 10, 30); // 303 / 365 → 41.5 of 50 expected

    [Theory]
    [InlineData(41, "On track · 9 to go")]
    [InlineData(44, "3 ahead of schedule · 6 to go")]
    [InlineData(39, "2 behind schedule · 11 to go")]
    [InlineData(50, "Goal reached")]
    [InlineData(52, "Goal reached · 2 over")]
    public void Status_says_where_the_reader_stands(int read, string expected) =>
        Assert.Equal(expected, ChallengeLabels.Status(ReadingPace.For(2026, read, 50, Today)));

    [Fact]
    public void Past_and_future_years_have_no_schedule()
    {
        Assert.Equal("1 book short of the goal", ChallengeLabels.Status(ReadingPace.For(2025, 9, 10, Today)));
        Assert.Equal("Not started yet", ChallengeLabels.Status(ReadingPace.For(2027, 0, 10, Today)));
        Assert.Null(ChallengeLabels.Rate(ReadingPace.For(2025, 9, 10, Today)));
    }

    [Theory]
    [InlineData(9, "about 1 a week")]     // 9 books in 63 days
    [InlineData(100, "about 2 a day")]
    [InlineData(2, "about 1 every 4 weeks")]  // one per 4.5 weeks
    public void Rate_picks_a_readable_unit(int remaining, string expected) =>
        Assert.Equal(expected, ChallengeLabels.Rate(ReadingPace.For(2026, 0, remaining, Today)));
}
