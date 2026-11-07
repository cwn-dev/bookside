namespace Bookside.Api.Words;

/// <summary>
/// Body of <c>POST /api/words</c>: <see cref="Core.Vocabulary.Services.WordInput"/>
/// plus <see cref="Book"/>, a title to link the word to instead of
/// <see cref="BookId"/>.
/// </summary>
internal sealed record ApiWordInput
{
    // Not required here: a missing term gets the service's field error.
    public string? Term { get; init; }

    public string? Language { get; init; }

    public Guid? BookId { get; init; }

    /// <summary>
    /// Exact book title (any case). Must match exactly one book.
    /// </summary>
    public string? Book { get; init; }

    public string? Context { get; init; }

    public int? PageNumber { get; init; }

    public string? Notes { get; init; }
}
