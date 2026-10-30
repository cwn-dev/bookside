namespace Bookside.UI.Tests;

public sealed class FinishedLabelsTests
{
    private static readonly DateOnly Finished = new(2026, 10, 30);

    [Fact]
    public void Summary_counts_both_the_start_and_finish_day()
    {
        Assert.Equal(
            "Finished 30 Oct 2026 · read in 28 days",
            FinishedLabels.Summary(new DateOnly(2026, 10, 3), Finished));
        Assert.Equal(
            "Finished 30 Oct 2026 · read in a day",
            FinishedLabels.Summary(Finished, Finished));
    }

    [Fact]
    public void Summary_without_a_start_date_shows_only_the_finish() =>
        Assert.Equal("Finished 30 Oct 2026", FinishedLabels.Summary(null, Finished));

    [Fact]
    public void Summary_without_a_finish_date_is_null() =>
        Assert.Null(FinishedLabels.Summary(new DateOnly(2026, 10, 3), null));
}
