using System.Globalization;
using Bookside.Core.Activity.Services;
using Bookside.Core.Books;

namespace Bookside.UI;

/// <summary>
/// One Monday-to-Sunday row of the activity calendar.
/// </summary>
/// <param name="Lanes">Bar rows the week needs (overlapping reads stack).</param>
public sealed record CalendarWeek(
    IReadOnlyList<CalendarDay> Days,
    IReadOnlyList<CalendarBar> Bars,
    int Lanes);

/// <param name="InMonth">False for the padding days of the months either side.</param>
/// <param name="Pills">The events not shown on a bar.</param>
public sealed record CalendarDay(
    DateOnly Date,
    bool InMonth,
    IReadOnlyList<ActivityEvent> Events,
    IReadOnlyList<ActivityEvent> Pills);

/// <summary>
/// The part of a read that falls in one week.
/// </summary>
/// <param name="Column">First day, 1 = Monday.</param>
/// <param name="Lane">Bar row within the week, from 0.</param>
/// <param name="Marks">The read's book events on the bar's days.</param>
public sealed record CalendarBar(
    ActivityRead Read,
    int Column,
    int Span,
    int Lane,
    bool ContinuesBefore,
    bool ContinuesAfter,
    IReadOnlyList<CalendarMark> Marks);

/// <summary>
/// A book's events on one day of its bar.
/// </summary>
/// <param name="Day">Days from the bar's first day, from 0.</param>
public sealed record CalendarMark(int Day, IReadOnlyList<ActivityEvent> Events);

/// <summary>
/// One line of a day's list: a read starting or ending, or an event.
/// </summary>
/// <param name="Icon">An <c>ActivityIcon</c> name.</param>
public sealed record CalendarItem(string Icon, string Label, string Text, string? Href);

/// <summary>
/// Lays out an <see cref="ActivityMonth"/> as weeks, and labels its items.
/// </summary>
public static class ActivityCalendar
{
    public static IReadOnlyList<CalendarWeek> Build(ActivityMonth month)
    {
        ArgumentNullException.ThrowIfNull(month);

        var first = new DateOnly(month.Year, month.Month, 1);
        var start = first.AddDays(-DaysFromMonday(first.DayOfWeek));
        var last = first.AddDays(DateTime.DaysInMonth(month.Year, month.Month) - 1);
        var events = month.Events.ToLookup(e => e.Date);
        var weeks = new List<CalendarWeek>();

        for (var monday = start; monday <= last; monday = monday.AddDays(7))
        {
            var sunday = monday.AddDays(6);
            var onBar = new HashSet<ActivityEvent>(ReferenceEqualityComparer.Instance);
            // Greedy lanes: each bar takes the first row that is free by its
            // first day. Reads arrive earliest start first.
            var laneEnds = new List<int>();
            var bars = new List<CalendarBar>();

            foreach (var read in month.Reads.Where(r => r.From <= sunday && r.To >= monday))
            {
                var from = read.From < monday ? monday : read.From;
                var to = read.To > sunday ? sunday : read.To;
                var column = from.DayNumber - monday.DayNumber + 1;
                var end = to.DayNumber - monday.DayNumber + 1;
                var lane = laneEnds.FindIndex(e => e < column);

                if (lane < 0)
                {
                    lane = laneEnds.Count;
                    laneEnds.Add(end);
                }
                else
                {
                    laneEnds[lane] = end;
                }

                // The book's own events move onto its bar. "Added" needs no
                // mark: the bar already shows the book.
                var marks = new List<CalendarMark>();

                for (var day = column; day <= end; day++)
                {
                    var own = events[monday.AddDays(day - 1)]
                        .Where(e => e.BookId == read.BookId && onBar.Add(e))
                        .Where(e => e.Kind != ActivityKind.BookAdded)
                        .ToList();

                    if (own.Count > 0)
                    {
                        marks.Add(new CalendarMark(day - column, own));
                    }
                }

                bars.Add(new CalendarBar(
                    read,
                    column,
                    end - column + 1,
                    lane,
                    read.From < monday,
                    read.To > sunday,
                    marks));
            }

            var days = Enumerable.Range(0, 7)
                .Select(i => monday.AddDays(i))
                .Select(d => new CalendarDay(
                    d,
                    d.Month == month.Month,
                    [.. events[d]],
                    [.. events[d].Where(e => !onBar.Contains(e))]))
                .ToList();

            weeks.Add(new CalendarWeek(days, bars, laneEnds.Count));
        }

        return weeks;
    }

    /// <summary>
    /// Everything that happened on <paramref name="date"/>: reads started,
    /// finished or stopped, then the events in order.
    /// </summary>
    public static IReadOnlyList<CalendarItem> ItemsOn(ActivityMonth month, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(month);

        var items = new List<CalendarItem>();

        foreach (var read in month.Reads)
        {
            var href = $"books/{read.BookId}";

            if (read.StartedOn == date)
            {
                items.Add(new CalendarItem("started", "Started", read.Title, href));
            }

            if (read.FinishedOn == date)
            {
                var stopped = read.Status == ReadingStatus.Abandoned;
                var stars = read.Rating is int r ? " " + new string('★', r) : string.Empty;
                items.Add(new CalendarItem(
                    stopped ? "stopped" : "finished",
                    stopped ? "Stopped" : "Finished",
                    read.Title + stars,
                    href));
            }
        }

        items.AddRange(month.Events.Where(e => e.Date == date).Select(Item));
        return items;
    }

    public static CalendarItem Item(ActivityEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var bookHref = e.BookId is Guid id ? $"books/{id}" : null;

        return e.Kind switch
        {
            ActivityKind.BookAdded => new("added", "Added", e.BookTitle ?? "", bookHref),
            ActivityKind.WordAdded => new(
                "word",
                "Word",
                e.BookTitle is null ? e.Text ?? "" : $"{e.Text} ({e.BookTitle})",
                $"words/{e.WordId}"),
            ActivityKind.Comment => new("comment", "Comment", $"{e.BookTitle}: {e.Text}", bookHref),
            ActivityKind.FlashCardsStarted => new(
                "cards",
                "Flash cards",
                e.Count == 1 ? "1 new word" : $"{e.Count} new words",
                "words/review"),
            ActivityKind.GoalReached => new(
                "goal",
                "Goal reached",
                string.Create(CultureInfo.CurrentCulture, $"{e.Count} books read"),
                $"challenge/{e.Date.Year}"),
            _ => throw new ArgumentOutOfRangeException(nameof(e), e.Kind, null),
        };
    }

    private static int DaysFromMonday(DayOfWeek day) => ((int)day + 6) % 7;
}
