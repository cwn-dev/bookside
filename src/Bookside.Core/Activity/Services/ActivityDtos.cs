using Bookside.Core.Books;

namespace Bookside.Core.Activity.Services;

/// <summary>
/// One month of activity.
/// </summary>
public sealed record ActivityMonth
{
    public required int Year { get; init; }

    public required int Month { get; init; }

    /// <summary>
    /// The server's local date; a read still in progress runs up to it.
    /// </summary>
    public required DateOnly Today { get; init; }

    /// <summary>
    /// Reading sessions whose span overlaps the month's calendar weeks
    /// (Monday to Sunday, so a few days of the months either side),
    /// earliest start first.
    /// </summary>
    public required IReadOnlyList<ActivityRead> Reads { get; init; }

    /// <summary>
    /// Other events in the same weeks, oldest first.
    /// </summary>
    public required IReadOnlyList<ActivityEvent> Events { get; init; }
}

/// <summary>
/// A reading session placed on the calendar.
/// </summary>
public sealed record ActivityRead
{
    public required Guid SessionId { get; init; }

    public required Guid BookId { get; init; }

    public required string Title { get; init; }

    /// <summary>
    /// The first day of the span: the start date, else the finish date.
    /// </summary>
    public required DateOnly From { get; init; }

    /// <summary>
    /// The last day of the span: the finish date, else today while reading,
    /// else the start date.
    /// </summary>
    public required DateOnly To { get; init; }

    public DateOnly? StartedOn { get; init; }

    public DateOnly? FinishedOn { get; init; }

    public required ReadingStatus Status { get; init; }

    public int? Rating { get; init; }
}

public enum ActivityKind
{
    BookAdded,
    WordAdded,
    Comment,
    FlashCardsStarted,
    GoalReached,
}

/// <summary>
/// A dated event other than a read. Which fields are set depends on
/// <see cref="Kind"/>.
/// </summary>
public sealed record ActivityEvent
{
    /// <summary>
    /// The server's local date.
    /// </summary>
    public required DateOnly Date { get; init; }

    public required ActivityKind Kind { get; init; }

    /// <summary>
    /// The book added or commented on, or the word's book.
    /// </summary>
    public Guid? BookId { get; init; }

    public string? BookTitle { get; init; }

    public Guid? WordId { get; init; }

    /// <summary>
    /// The word's term, or the start of the comment in display form.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Words started in flash cards that day, or the goal reached.
    /// </summary>
    public int? Count { get; init; }
}
