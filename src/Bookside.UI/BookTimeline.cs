using Bookside.Core.Books.Services;

namespace Bookside.UI;

/// <summary>One row of a book's reading log.</summary>
public abstract record TimelineEntry(DateTime SortKey, Guid Id);

/// <summary>The session card, placed at its finish date (or start date / creation when it has no finish).</summary>
public sealed record SessionTimelineEntry(DateTime SortKey, ReadingSessionDto Session) : TimelineEntry(SortKey, Session.Id);

/// <summary>"Started reading" marker for a session whose card sits at a later finish date.</summary>
public sealed record SessionStartTimelineEntry(DateTime SortKey, ReadingSessionDto Session) : TimelineEntry(SortKey, Session.Id);

public sealed record CommentTimelineEntry(DateTime SortKey, BookCommentDto Comment) : TimelineEntry(SortKey, Comment.Id);

/// <summary>
/// Merges a book's reading sessions and comments into one newest-first list. Sort keys are local times in
/// <c>timeZone</c> (the server's, see PROJECT_STATE). Session dates have no time of day: when the session was
/// created/updated on that same date its timestamp is used, otherwise the end of the day, so a back-dated read sorts
/// above comments from the same day.
/// </summary>
public static class BookTimeline
{
    public static IReadOnlyList<TimelineEntry> Build(BookDetails book, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(timeZone);

        var entries = new List<TimelineEntry>(book.ReadingSessions.Count * 2 + book.Comments.Count);
        foreach (var session in book.ReadingSessions)
        {
            if (session.FinishedOn is DateOnly finished)
            {
                entries.Add(new SessionTimelineEntry(At(finished, session.UpdatedAtUtc, timeZone), session));
                if (session.StartedOn is DateOnly started && started != finished)
                {
                    entries.Add(new SessionStartTimelineEntry(At(started, session.CreatedAtUtc, timeZone), session));
                }
            }
            else if (session.StartedOn is DateOnly started)
            {
                entries.Add(new SessionTimelineEntry(At(started, session.CreatedAtUtc, timeZone), session));
            }
            else
            {
                entries.Add(new SessionTimelineEntry(ToLocal(session.CreatedAtUtc, timeZone), session));
            }
        }

        foreach (var comment in book.Comments)
        {
            entries.Add(new CommentTimelineEntry(ToLocal(comment.CreatedAtUtc, timeZone), comment));
        }

        // Ties: UUIDv7 ids are time-ordered, so the later-created item goes first. Start markers come after their card.
        return entries
            .OrderByDescending(e => e.SortKey)
            .ThenBy(e => e is SessionStartTimelineEntry)
            .ThenByDescending(e => e.Id)
            .ToList();
    }

    public static DateTime ToLocal(DateTime utc, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);

    private static DateTime At(DateOnly date, DateTime stampUtc, TimeZoneInfo timeZone)
    {
        var stamp = ToLocal(stampUtc, timeZone);
        return DateOnly.FromDateTime(stamp) == date ? stamp : date.ToDateTime(TimeOnly.MaxValue);
    }
}
