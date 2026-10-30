using System.Globalization;
using Bookside.Core.Challenge;

namespace Bookside.UI;

/// <summary>
/// Display text for the reading challenge.
/// </summary>
public static class ChallengeLabels
{
    /// <summary>
    /// "1 book", "12 books".
    /// </summary>
    public static string Books(int count) =>
        count == 1 ? "1 book" : string.Create(CultureInfo.CurrentCulture, $"{count} books");

    /// <summary>
    /// Where the reader stands: "On track · 27 to go", "3 ahead of schedule",
    /// "Goal reached", "2 short of the goal", "Not started yet".
    /// </summary>
    public static string Status(ReadingPace pace)
    {
        ArgumentNullException.ThrowIfNull(pace);

        return pace.State switch
        {
            ChallengeState.Future => "Not started yet",
            ChallengeState.Reached when pace.ReadCount > pace.TargetBooks =>
                $"Goal reached · {pace.ReadCount - pace.TargetBooks} over",
            ChallengeState.Reached => "Goal reached",
            ChallengeState.Ended => $"{Books(pace.Remaining)} short of the goal",
            _ => $"{Schedule(pace.Ahead)} · {pace.Remaining} to go",
        };
    }

    /// <summary>
    /// The rate needed to finish on time: "about 2 a week", "about 1 a day",
    /// "about 1 every 3 weeks". Null when the challenge isn't in progress.
    /// </summary>
    public static string? Rate(ReadingPace pace)
    {
        ArgumentNullException.ThrowIfNull(pace);

        return pace.PerWeekNeeded switch
        {
            null => null,
            >= 7 => $"about {Math.Round(pace.PerWeekNeeded.Value / 7)} a day",
            >= 0.75 => $"about {Math.Max(1, Math.Round(pace.PerWeekNeeded.Value))} a week",
            var perWeek => $"about 1 every {Math.Round(1 / perWeek.Value)} weeks",
        };
    }

    private static string Schedule(int ahead) => ahead switch
    {
        0 => "On track",
        > 0 => $"{ahead} ahead of schedule",
        _ => $"{-ahead} behind schedule",
    };
}
