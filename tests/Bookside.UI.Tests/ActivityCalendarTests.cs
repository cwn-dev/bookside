using Bookside.Core.Activity.Services;
using Bookside.Core.Books;

namespace Bookside.UI.Tests;

public sealed class ActivityCalendarTests
{
    // June 2026 starts on a Monday and ends on a Tuesday.
    private static ActivityMonth June(IReadOnlyList<ActivityRead> reads, params ActivityEvent[] events) =>
        new()
        {
            Year = 2026,
            Month = 6,
            Today = new DateOnly(2026, 6, 15),
            Reads = reads,
            Events = events,
        };

    private static ActivityRead Read(string title, DateOnly from, DateOnly to) =>
        new()
        {
            SessionId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            Title = title,
            From = from,
            To = to,
            StartedOn = from,
            FinishedOn = to,
            Status = ReadingStatus.Finished,
        };

    [Fact]
    public void Weeks_run_monday_to_sunday_padded_with_the_months_either_side()
    {
        var weeks = ActivityCalendar.Build(June([]));

        Assert.Equal(5, weeks.Count);
        Assert.Equal(new DateOnly(2026, 6, 1), weeks[0].Days[0].Date);
        Assert.Equal(new DateOnly(2026, 7, 5), weeks[4].Days[6].Date);
        Assert.False(weeks[4].Days[2].InMonth);
        Assert.True(weeks[4].Days[1].InMonth);
    }

    [Fact]
    public void A_read_across_weeks_is_split_into_continuing_bars()
    {
        var weeks = ActivityCalendar.Build(June([Read("Mort", new(2026, 6, 5), new(2026, 6, 10))]));

        var first = Assert.Single(weeks[0].Bars);
        var second = Assert.Single(weeks[1].Bars);
        Assert.Equal((5, 3, false, true), (first.Column, first.Span, first.ContinuesBefore, first.ContinuesAfter));
        Assert.Equal((1, 3, true, false), (second.Column, second.Span, second.ContinuesBefore, second.ContinuesAfter));
    }

    [Fact]
    public void Overlapping_reads_stack_and_free_lanes_are_reused()
    {
        var weeks = ActivityCalendar.Build(June(
        [
            Read("A", new(2026, 6, 1), new(2026, 6, 3)),
            Read("B", new(2026, 6, 2), new(2026, 6, 4)),
            Read("C", new(2026, 6, 5), new(2026, 6, 6)),
        ]));

        Assert.Equal([0, 1, 0], weeks[0].Bars.Select(b => b.Lane));
        Assert.Equal(2, weeks[0].Lanes);
        Assert.Equal(0, weeks[1].Lanes);
    }

    [Fact]
    public void A_day_lists_starts_finishes_then_events()
    {
        var day = new DateOnly(2026, 6, 3);
        var stopped = Read("Eric", new(2026, 6, 1), day) with { Status = ReadingStatus.Abandoned };
        var month = June(
            [Read("Mort", day, new(2026, 6, 9)) with { Rating = 4 }, stopped],
            new ActivityEvent { Date = day, Kind = ActivityKind.FlashCardsStarted, Count = 2 },
            new ActivityEvent { Date = day.AddDays(1), Kind = ActivityKind.BookAdded, BookTitle = "Other" });

        var items = ActivityCalendar.ItemsOn(month, day);

        Assert.Equal(
            [("Started", "Mort"), ("Stopped", "Eric"), ("Flash cards", "2 new words")],
            items.Select(i => (i.Label, i.Text)));
        Assert.Equal("Finished", ActivityCalendar.ItemsOn(month, new(2026, 6, 9))[0].Label);
        Assert.Equal("Mort ★★★★", ActivityCalendar.ItemsOn(month, new(2026, 6, 9))[0].Text);
    }
}
