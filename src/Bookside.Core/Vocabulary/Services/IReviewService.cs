using Bookside.Core.Common;

namespace Bookside.Core.Vocabulary.Services;

/// <summary>
/// Flash-card review of words that have a definition, scheduled by
/// <see cref="ReviewScheduler"/>. "Today" is the server's local date.
/// </summary>
public interface IReviewService
{
    Task<ReviewStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Due words (longest overdue first), then new words (oldest added first)
    /// up to what is left of today's new-word limit.
    /// </summary>
    Task<IReadOnlyList<ReviewCard>> GetQueueAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cards for a practice round, which doesn't change the schedule:
    /// today's forgotten words (all of them), or up to
    /// <see cref="ReviewScheduler.PracticeRoundSize"/> random or most-forgotten
    /// words.
    /// </summary>
    Task<IReadOnlyList<ReviewCard>> GetPracticeCardsAsync(
        PracticeSet words,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a review and schedules the word. Only the first review of a
    /// word each day counts; later ones return the existing schedule.
    /// </summary>
    Task<Result<ReviewOutcome>> RecordReviewAsync(
        Guid wordId,
        bool knew,
        CancellationToken cancellationToken = default);
}
