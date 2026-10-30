namespace Bookside.Core.Vocabulary;

/// <summary>
/// Flash-card review state of a word (1:1 with <see cref="VocabularyWord"/>,
/// keyed by WordId). A word with no row is a new card. Dates are local dates
/// (the server's time zone), like reading session dates.
/// </summary>
public class WordReview
{
    public Guid WordId { get; set; }

    public VocabularyWord Word { get; set; } = null!;

    /// <summary>
    /// The first local date the word is due again.
    /// </summary>
    public DateOnly DueOn { get; set; }

    public int IntervalDays { get; set; }

    /// <summary>
    /// SM-2 ease factor; see <see cref="ReviewScheduler"/>.
    /// </summary>
    public double Ease { get; set; } = ReviewScheduler.InitialEase;

    /// <summary>
    /// Times remembered in a row since the last lapse.
    /// </summary>
    public int Repetitions { get; set; }

    /// <summary>
    /// Times forgotten after being learnt.
    /// </summary>
    public int Lapses { get; set; }

    /// <summary>
    /// Counts the word against that day's new-card limit.
    /// </summary>
    public DateOnly FirstReviewedOn { get; set; }

    public DateOnly LastReviewedOn { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
