namespace Bookside.UI.Tests;

public sealed class ReviewLabelsTests
{
    [Theory]
    [InlineData(0, "today")]
    [InlineData(1, "tomorrow")]
    [InlineData(6, "in 6 days")]
    [InlineData(44, "in 44 days")]
    [InlineData(45, "in 2 months")]
    [InlineData(95, "in 3 months")]
    [InlineData(364, "in 12 months")]
    [InlineData(365, "in 1 year")]
    [InlineData(1000, "in 3 years")]
    public void Interval_reads_naturally(int days, string expected) =>
        Assert.Equal(expected, ReviewLabels.Interval(days));

    [Fact]
    public void Due_date_is_relative_when_close()
    {
        var today = new DateOnly(2026, 10, 27);

        Assert.Equal("today", ReviewLabels.DueOn(today, today));
        Assert.Equal("tomorrow", ReviewLabels.DueOn(today.AddDays(1), today));
        Assert.Equal("on 2 Nov 2026", ReviewLabels.DueOn(today.AddDays(6), today));
    }
}
