using Bookside.Core.Books.Services;
using Bookside.Core.Vocabulary.Services;

namespace Bookside.Core.Search.Services;

/// <summary>
/// The first few matches of one kind; <see cref="HasMore"/> when more matched.
/// </summary>
public sealed record SearchSection<T>(IReadOnlyList<T> Items, bool HasMore);

public sealed record SearchResults
{
    /// <summary>
    /// Shorter queries (after trimming) aren't searched: they match too much.
    /// </summary>
    public const int MinQueryLength = 3;

    /// <summary>
    /// The trimmed query the results are for.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Titles starting with the query first, then by title.
    /// </summary>
    public required SearchSection<BookSummary> Books { get; init; }

    /// <summary>
    /// Terms starting with the query first, then by term.
    /// </summary>
    public required SearchSection<WordSummary> Words { get; init; }

    /// <summary>
    /// Names starting with the query first, then by name.
    /// </summary>
    public required SearchSection<TagDto> Tags { get; init; }

    /// <summary>
    /// Newest first.
    /// </summary>
    public required SearchSection<CommentSearchHit> Comments { get; init; }

    public bool IsEmpty =>
        Books.Items.Count == 0
        && Words.Items.Count == 0
        && Tags.Items.Count == 0
        && Comments.Items.Count == 0;

    public static SearchResults EmptyFor(string query) => new()
    {
        Query = query,
        Books = new SearchSection<BookSummary>([], HasMore: false),
        Words = new SearchSection<WordSummary>([], HasMore: false),
        Tags = new SearchSection<TagDto>([], HasMore: false),
        Comments = new SearchSection<CommentSearchHit>([], HasMore: false),
    };
}

public sealed record CommentSearchHit
{
    public required Guid CommentId { get; init; }

    public required Guid BookId { get; init; }

    public required string BookTitle { get; init; }

    /// <summary>
    /// Part of the comment around the match, with tags in display form (see
    /// <see cref="SearchSnippet"/>).
    /// </summary>
    public required string Snippet { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
