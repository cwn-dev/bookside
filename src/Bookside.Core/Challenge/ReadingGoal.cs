using Bookside.Core.Common;

namespace Bookside.Core.Challenge;

/// <summary>
/// How many books the user aims to finish in a year. One per year.
/// </summary>
public class ReadingGoal : Entity
{
    public const int MinYear = 1;
    public const int MaxYear = 9999;
    public const int MinTarget = 1;
    public const int MaxTarget = 1000;

    public int Year { get; set; }

    public int TargetBooks { get; set; }
}
