using System.Globalization;

namespace Bookside.UI;

/// <summary>
/// Display text for the "Finished!" popup.
/// </summary>
public static class FinishedLabels
{
    /// <summary>
    /// The finish date and how long the read took, e.g. "Finished 30 Oct 2026
    /// · read in 28 days"; null without a finish date.
    /// </summary>
    public static string? Summary(DateOnly? startedOn, DateOnly? finishedOn)
    {
        if (finishedOn is not DateOnly finished)
        {
            return null;
        }

        var text = "Finished " + finished.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        if (startedOn is not DateOnly started || started > finished)
        {
            return text;
        }

        // Both days count: started and finished on the same day is one day.
        var days = finished.DayNumber - started.DayNumber + 1;
        return text + (days == 1 ? " · read in a day" : $" · read in {days} days");
    }
}
