using Bookside.Core.Books;
using Bookside.Core.Books.Services;

namespace Bookside.UI.Tests;

public sealed class BookTimelineTests
{
    private static readonly Guid BookId = Guid.CreateVersion7();

    [Fact]
    public void Comments_during_a_read_sit_between_its_finish_card_and_start_marker()
    {
        // Logged afterwards, so the session's own timestamps are on neither of its dates.
        var read = Session(new(2026, 3, 12), new(2026, 3, 30), createdAndUpdated: Utc(2026, 4, 1, 9, 0));
        var before = Comment(Utc(2026, 1, 2, 19, 40));
        var early = Comment(Utc(2026, 3, 14, 8, 2));
        var late = Comment(Utc(2026, 3, 21, 22, 10));

        var timeline = BookTimeline.Build(Book([read], [late, early, before]), TimeZoneInfo.Utc);

        Assert.Collection(
            timeline,
            e => Assert.Equal(read, Assert.IsType<SessionTimelineEntry>(e).Session),
            e => Assert.Equal(late, Assert.IsType<CommentTimelineEntry>(e).Comment),
            e => Assert.Equal(early, Assert.IsType<CommentTimelineEntry>(e).Comment),
            e => Assert.Equal(read, Assert.IsType<SessionStartTimelineEntry>(e).Session),
            e => Assert.Equal(before, Assert.IsType<CommentTimelineEntry>(e).Comment));
    }

    [Fact]
    public void Same_day_entries_use_the_session_timestamp_when_it_was_made_that_day()
    {
        var reading = Session(new(2026, 10, 24), null, createdAndUpdated: Utc(2026, 10, 24, 9, 0), ReadingStatus.Reading);
        var after = Comment(Utc(2026, 10, 24, 10, 0));
        var before = Comment(Utc(2026, 10, 24, 8, 0));

        var timeline = BookTimeline.Build(Book([reading], [after, before]), TimeZoneInfo.Utc);

        Assert.Equal([after.Id, reading.Id, before.Id], timeline.Select(e => e.Id));
    }

    [Fact]
    public void Back_dated_session_sorts_at_the_end_of_its_day()
    {
        var pastRead = Session(new(2026, 3, 1), new(2026, 3, 30), createdAndUpdated: Utc(2026, 10, 1, 9, 0));
        var sameDay = Comment(Utc(2026, 3, 30, 23, 30));

        var timeline = BookTimeline.Build(Book([pastRead], [sameDay]), TimeZoneInfo.Utc);

        Assert.Equal([pastRead.Id, sameDay.Id, pastRead.Id], timeline.Select(e => e.Id));
    }

    [Fact]
    public void Sort_keys_are_local_to_the_given_time_zone()
    {
        var plusTwo = TimeZoneInfo.CreateCustomTimeZone("UTC+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");
        var pastRead = Session(null, new(2026, 3, 30), createdAndUpdated: Utc(2026, 10, 1, 9, 0));
        var comment = Comment(Utc(2026, 3, 30, 23, 30)); // 31 March, 01:30 at UTC+2

        var timeline = BookTimeline.Build(Book([pastRead], [comment]), plusTwo);

        Assert.Equal([comment.Id, pastRead.Id], timeline.Select(e => e.Id));
        Assert.Equal(new DateTime(2026, 3, 31, 1, 30, 0), timeline[0].SortKey);
    }

    [Fact]
    public void Single_day_and_undated_sessions_get_one_entry()
    {
        var oneDay = Session(new(2026, 5, 1), new(2026, 5, 1), createdAndUpdated: Utc(2026, 5, 2, 9, 0));
        var undated = Session(null, null, createdAndUpdated: Utc(2026, 6, 1, 12, 0), ReadingStatus.Abandoned);

        var timeline = BookTimeline.Build(Book([oneDay, undated], []), TimeZoneInfo.Utc);

        Assert.Equal([undated.Id, oneDay.Id], timeline.Select(e => e.Id));
        Assert.All(timeline, e => Assert.IsType<SessionTimelineEntry>(e));
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static ReadingSessionDto Session(
        DateOnly? started,
        DateOnly? finished,
        DateTime createdAndUpdated,
        ReadingStatus status = ReadingStatus.Finished) => new()
        {
            Id = Guid.CreateVersion7(),
            BookId = BookId,
            StartedOn = started,
            FinishedOn = finished,
            Status = status,
            CreatedAtUtc = createdAndUpdated,
            UpdatedAtUtc = createdAndUpdated,
        };

    private static BookCommentDto Comment(DateTime createdUtc) => new()
    {
        Id = Guid.CreateVersion7(),
        BookId = BookId,
        Text = "Comment",
        CreatedAtUtc = createdUtc,
        UpdatedAtUtc = createdUtc,
    };

    private static BookDetails Book(IReadOnlyList<ReadingSessionDto> sessions, IReadOnlyList<BookCommentDto> comments) => new()
    {
        Id = BookId,
        Title = "Dune",
        Authors = [],
        ReadingSessions = sessions,
        Comments = comments,
    };
}
