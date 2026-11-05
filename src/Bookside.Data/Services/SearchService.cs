using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.Core.Search;
using Bookside.Core.Search.Services;
using Bookside.Core.Vocabulary;
using Bookside.Core.Vocabulary.Services;
using Microsoft.EntityFrameworkCore;

namespace Bookside.Data.Services;

/// <summary>
/// One context, one query per section. Each section fetches one row more than
/// it shows, to know whether there are more without a COUNT. Matching is
/// <c>LIKE '%q%'</c> (no index can help; fine at personal-library size).
/// </summary>
internal sealed class SearchService(IDbContextFactory<BooksideDbContext> dbFactory)
    : ISearchService
{
    internal const int MaxBooks = 6;
    internal const int MaxWords = 6;
    internal const int MaxTags = 8;
    internal const int MaxComments = 5;

    // LIKE also finds matches inside unsearchable tags (pages), which are
    // dropped in memory afterwards.
    // shortcut: a 4× over-fetch; if pages crowd out real matches, filter in SQL.
    private const int CommentCandidates = MaxComments * 4 + 1;

    public async Task<SearchResults> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = query.Trim();

        if (q.Length < SearchResults.MinQueryLength)
        {
            return SearchResults.EmptyFor(q);
        }

        await using var db = await dbFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return new SearchResults
        {
            Query = q,
            Books = await SearchBooksAsync(db, q, cancellationToken).ConfigureAwait(false),
            Words = await SearchWordsAsync(db, q, cancellationToken).ConfigureAwait(false),
            Tags = await SearchTagsAsync(db, q, cancellationToken).ConfigureAwait(false),
            Comments = await SearchCommentsAsync(db, q, cancellationToken).ConfigureAwait(false),
        };
    }

    private static async Task<SearchSection<BookSummary>> SearchBooksAsync(
        BooksideDbContext db,
        string q,
        CancellationToken cancellationToken)
    {
        var prefix = LikePattern.StartsWith(q);
        var books = LibraryService.ApplySearch(db.Books.AsNoTracking(), q)
            .OrderByDescending(b =>
                EF.Functions.Like(b.Title, prefix, LikePattern.EscapeCharacter))
            .ThenBy(b => b.Title)
            .ThenBy(b => b.Id);

        var rows = await LibraryService.ToSummaries(books)
            .Take(MaxBooks + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToSection(rows, MaxBooks);
    }

    private static async Task<SearchSection<WordSummary>> SearchWordsAsync(
        BooksideDbContext db,
        string q,
        CancellationToken cancellationToken)
    {
        var normalized = TermNormalizer.Normalize(q);
        var pattern = LikePattern.Contains(normalized);
        var prefix = LikePattern.StartsWith(normalized);

        var rows = await db.VocabularyWords.AsNoTracking()
            .Where(w =>
                EF.Functions.Like(w.NormalizedTerm, pattern, LikePattern.EscapeCharacter))
            .OrderByDescending(w =>
                EF.Functions.Like(w.NormalizedTerm, prefix, LikePattern.EscapeCharacter))
            .ThenBy(w => w.NormalizedTerm)
            .ThenBy(w => w.Id)
            .Take(MaxWords + 1)
            .Include(w => w.Book)
            .Include(w => w.DictionaryEntry)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToSection([.. rows.Select(VocabularyService.ToSummary)], MaxWords);
    }

    private static async Task<SearchSection<TagDto>> SearchTagsAsync(
        BooksideDbContext db,
        string q,
        CancellationToken cancellationToken)
    {
        var normalized = TermNormalizer.Normalize(q);
        var pattern = LikePattern.Contains(normalized);
        var prefix = LikePattern.StartsWith(normalized);

        var rows = await db.Tags.AsNoTracking()
            .Where(t =>
                EF.Functions.Like(t.NormalizedName, pattern, LikePattern.EscapeCharacter))
            .OrderByDescending(t =>
                EF.Functions.Like(t.NormalizedName, prefix, LikePattern.EscapeCharacter))
            .ThenBy(t => t.NormalizedName)
            .Take(MaxTags + 1)
            .Select(t => new TagDto
            {
                Id = t.Id,
                Name = t.Name,
                BookCount = t.Books.Count,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToSection(rows, MaxTags);
    }

    private static async Task<SearchSection<CommentSearchHit>> SearchCommentsAsync(
        BooksideDbContext db,
        string q,
        CancellationToken cancellationToken)
    {
        var pattern = LikePattern.Contains(q);
        // Tags are stored bracketed (@[Fred Test]) but typed as shown (@Fred).
        var tagPattern = LikePattern.Contains(CommentTagSyntax.ToStoredSearch(q));

        var rows = await db.BookComments.AsNoTracking()
            .Where(c =>
                EF.Functions.Like(c.Text, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.Like(c.Text, tagPattern, LikePattern.EscapeCharacter))
            .OrderByDescending(c => c.CreatedAtUtc)
            .ThenByDescending(c => c.Id)
            .Take(CommentCandidates)
            .Select(c => new
            {
                c.Id,
                c.BookId,
                BookTitle = c.Book.Title,
                c.Text,
                c.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hits = new List<CommentSearchHit>();

        foreach (var c in rows)
        {
            if (SearchSnippet.Around(c.Text, q) is not { } snippet)
            {
                continue; // matched only inside an unsearchable tag
            }

            hits.Add(new CommentSearchHit
            {
                CommentId = c.Id,
                BookId = c.BookId,
                BookTitle = c.BookTitle,
                Snippet = snippet,
                CreatedAtUtc = c.CreatedAtUtc,
            });

            if (hits.Count > MaxComments)
            {
                break;
            }
        }

        return ToSection(hits, MaxComments);
    }

    private static SearchSection<T> ToSection<T>(List<T> rows, int max) =>
        rows.Count > max
            ? new SearchSection<T>(rows[..max], HasMore: true)
            : new SearchSection<T>(rows, HasMore: false);
}
