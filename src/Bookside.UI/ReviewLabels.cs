using System.Globalization;

namespace Bookside.UI;

/// <summary>
/// Display text for flash-card review scheduling.
/// </summary>
public static class ReviewLabels
{
    /// <summary>
    /// When a word comes back after <paramref name="days"/>, e.g.
    /// "tomorrow", "in 6 days", "in 3 months".
    /// </summary>
    public static string Interval(int days) => days switch
    {
        <= 0 => "today",
        1 => "tomorrow",
        < 45 => $"in {days} days",
        < 365 => Plural(Math.Max(2, (int)Math.Round(days / 30.0)), "month"),
        _ => Plural((int)Math.Round(days / 365.0), "year"),
    };

    /// <summary>
    /// A due date relative to <paramref name="today"/>: "today", "tomorrow",
    /// else e.g. "on 3 Nov 2026".
    /// </summary>
    public static string DueOn(DateOnly dueOn, DateOnly today) =>
        (dueOn.DayNumber - today.DayNumber) switch
        {
            <= 0 => "today",
            1 => "tomorrow",
            _ => "on " + dueOn.ToString("d MMM yyyy", CultureInfo.CurrentCulture),
        };

    private static string Plural(int count, string unit) =>
        count == 1 ? $"in 1 {unit}" : $"in {count} {unit}s";
}
