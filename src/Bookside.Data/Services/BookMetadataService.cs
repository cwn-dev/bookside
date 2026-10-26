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
        List<string>? failures = null;
        foreach (var provider in _providers)
        {
            IReadOnlyList<BookMetadata> results;
            try
            {
                results = isbn is null
                    ? await provider
                        .SearchAsync(text, MaxResults, cancellationToken)
                        .ConfigureAwait(false)
                    : await FindByIsbnAsync(provider, isbn, cancellationToken)
                        .ConfigureAwait(false);
            }
            catch (BookLookupException ex)
            {
                (failures ??= []).Add(ex.Message);
                continue;
            }

            var usable = results
                .Select(Clean)
                .OfType<BookMetadata>()
                .Take(MaxResults)
                .ToList();

            if (usable.Count > 0)
            {
                return usable;
            }
        }

        return failures is null
            ? Array.Empty<BookMetadata>()
            : ServiceError.Unavailable(
                ErrorCodes.BookLookupUnavailable,
                $"Book search is unavailable: {string.Join(" ", failures)}");
    }

    public async Task<Result<CoverInput>> DownloadCoverAsync(
        BookMetadata book,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);

        var provider = _providers.FirstOrDefault(p => p.Source == book.Source);
        if (book.CoverImageUrl is not { } url || provider is null)
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
