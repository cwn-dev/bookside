using Bookside.Core.Books;
using Bookside.Core.Books.Lookup;
using Bookside.Core.Books.Services;
using Bookside.Core.Common;

namespace Bookside.Data.Services;

internal sealed class BookMetadataService(IEnumerable<IBookLookupService> lookups)
    : IBookMetadataService
{
    private const int MaxResults = 10;

    private readonly IBookLookupService[] _providers =
        lookups.ToArray() is { Length: > 0 } providers
            ? providers
            : throw new InvalidOperationException("No IBookLookupService is registered.");

    public async Task<Result<IReadOnlyList<BookMetadata>>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var v = new InputValidator();
        var text = v.Required(nameof(query), query, Book.TitleMaxLength);
        if (!v.IsValid)
        {
            return v.ToError();
        }

        var isbn = ToIsbn13(Isbn.Clean(text));
        var searches = _providers.Select(p =>
            SearchProviderAsync(p, text, isbn, cancellationToken));
        var outcomes = await Task.WhenAll(searches).ConfigureAwait(false);

        var lists = outcomes.Select(o => o.Results).ToList();
        if (lists.Any(l => l.Count > 0))
        {
            return Interleave(lists);
        }

        var failures = outcomes.Select(o => o.Failure).OfType<string>().ToList();
        return failures.Count == 0
            ? Array.Empty<BookMetadata>()
            : ServiceError.Unavailable(
                ErrorCodes.BookLookupUnavailable,
                $"Book search is unavailable: {string.Join(" ", failures)}");
    }

    public async Task<Result<BookMetadata>> GetDetailsAsync(
        BookMetadata book,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);

        if (Provider(book) is not { } provider)
        {
            return ServiceError.Validation(
                nameof(BookMetadata.Source),
                "Unknown book source.");
        }

        BookMetadata details;
        try
        {
            details = await provider
                .GetDetailsAsync(book, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (BookLookupException ex)
        {
            return ServiceError.Unavailable(
                ErrorCodes.BookLookupUnavailable,
                $"The book's details could not be loaded: {ex.Message}");
        }

        return Clean(details) is { } clean
            ? clean with { Description = clean.Description ?? book.Description }
            : book;
    }

    public async Task<Result<CoverInput>> DownloadCoverAsync(
        BookMetadata book,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);

        if (book.CoverImageUrl is not { } url || Provider(book) is not { } provider)
        {
            return ServiceError.Validation(
                nameof(BookMetadata.CoverImageUrl),
                "No cover to download.");
        }

        try
        {
            return await provider
                .DownloadCoverAsync(url, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (BookLookupException ex)
        {
            return ServiceError.Unavailable(
                ErrorCodes.BookLookupUnavailable,
                $"The cover could not be downloaded: {ex.Message}");
        }
    }

    private IBookLookupService? Provider(BookMetadata book) =>
        _providers.FirstOrDefault(p => p.Source == book.Source);

    /// <summary>
    /// One provider's cleaned results, or its failure message.
    /// </summary>
    private static async Task<(IReadOnlyList<BookMetadata> Results, string? Failure)>
        SearchProviderAsync(
            IBookLookupService provider,
            string text,
            string? isbn13,
            CancellationToken cancellationToken)
    {
        IReadOnlyList<BookMetadata> results;
        try
        {
            results = isbn13 is null
                ? await provider
                    .SearchAsync(text, MaxResults, cancellationToken)
                    .ConfigureAwait(false)
                : await FindByIsbnAsync(provider, isbn13, cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (BookLookupException ex)
        {
            return ([], ex.Message);
        }

        var usable = results
            .Select(Clean)
            .OfType<BookMetadata>()
            .Take(MaxResults)
            .ToList();

        return (usable, null);
    }

    /// <summary>
    /// Takes the providers' results in turns (first of each, then second of
    /// each, …), so every provider's best matches are near the top.
    /// </summary>
    private static List<BookMetadata> Interleave(List<IReadOnlyList<BookMetadata>> lists) =>
        [
            .. Enumerable
                .Range(0, lists.Max(l => l.Count))
                .SelectMany(i => lists.Where(l => i < l.Count).Select(l => l[i])),
        ];

    /// <summary>
    /// The ISBN searched for stands in when the provider's record has no
    /// ISBN-13.
    /// </summary>
    private static async Task<IReadOnlyList<BookMetadata>> FindByIsbnAsync(
        IBookLookupService provider,
        string isbn13,
        CancellationToken cancellationToken)
    {
        var found = await provider
            .FindByIsbnAsync(isbn13, cancellationToken)
            .ConfigureAwait(false);

        return found is null ? [] : [found with { Isbn13 = found.Isbn13 ?? isbn13 }];
    }

    private static string? ToIsbn13(string clean) =>
        Isbn.IsValidIsbn13(clean) ? clean
        : Isbn.IsValidIsbn10(clean) ? Isbn.ToIsbn13(clean)
        : null;

    /// <summary>
    /// Drops values the book form would reject (too long, bad ISBN), so picking
    /// a result never fails validation on fields the user didn't type. A result
    /// without a usable title is dropped.
    /// </summary>
    private static BookMetadata? Clean(BookMetadata m)
    {
        var title = Fit(m.Title, Book.TitleMaxLength);
        if (title is null
            || m.Source.Length > Book.MetadataSourceMaxLength
            || m.SourceId.Length > Book.MetadataSourceIdMaxLength)
        {
            return null;
        }

        return m with
        {
            Title = title,
            Subtitle = Fit(m.Subtitle, Book.SubtitleMaxLength),
            Authors =
            [
                .. m.Authors
                    .Select(a => Fit(a, Author.NameMaxLength))
                    .OfType<string>()
                    .Distinct(),
            ],
            PageCount = m.PageCount > 0 ? m.PageCount : null,
            Isbn13 = ValidOrNull(m.Isbn13, Isbn.IsValidIsbn13),
            Isbn10 = ValidOrNull(m.Isbn10, Isbn.IsValidIsbn10),
            Publisher = Fit(m.Publisher, Book.PublisherMaxLength),
            Description = Fit(m.Description, Book.DescriptionMaxLength),
        };

        static string? Fit(string? value, int max) =>
            value?.Trim() is { Length: > 0 } t && t.Length <= max ? t : null;

        static string? ValidOrNull(string? isbn, Func<string, bool> isValid) =>
            isbn is not null && Isbn.Clean(isbn) is var clean && isValid(clean) ? clean : null;
    }
}
