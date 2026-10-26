using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bookside.Core.Books.Lookup;
using Bookside.Core.Books.Services;

namespace Bookside.Integrations.Books;

/// <summary>
/// openlibrary.org client (no key). ISBN lookups return an edition (with ISBNs
/// and publisher); text searches return works, which have no ISBN, publisher or
/// description (an edition can't be told apart from a work's list).
/// </summary>
internal sealed class OpenLibraryClient(HttpClient http, BookCoverDownloader covers)
    : IBookLookupService
{
    public const string SourceId = "openlibrary";

    private const string SearchFields =
        "key,title,subtitle,author_name,first_publish_year,cover_i,number_of_pages_median";

    private static readonly IReadOnlySet<string> CoverHosts = new HashSet<string>(
        ["covers.openlibrary.org"],
        StringComparer.OrdinalIgnoreCase);

    public string Source => SourceId;

    public async Task<BookMetadata?> FindByIsbnAsync(
        string isbn13,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn13);

        var path = new Uri(
            $"api/books?bibkeys=ISBN:{Uri.EscapeDataString(isbn13)}&jscmd=data&format=json",
            UriKind.Relative);

        var editions = await GetAsync(
                path,
                OpenLibraryJsonContext.Default.DictionaryStringOlEdition,
                cancellationToken)
            .ConfigureAwait(false);

        if (editions.Values.FirstOrDefault() is not { Key: { } key, Title: { } title } e)
        {
            return null;
        }

        return new BookMetadata
        {
            Source = SourceId,
            SourceId = key,
            Title = title,
            Subtitle = e.Subtitle,
            Authors = Names(e.Authors),
            PageCount = e.NumberOfPages,
            Isbn13 = First(e.Identifiers?.Isbn13),
            Isbn10 = First(e.Identifiers?.Isbn10),
            Publisher = First(Names(e.Publishers)),
            PublishedYear = BookHttp.Year(e.PublishDate),
            CoverImageUrl = Uri.TryCreate(e.Cover?.Large, UriKind.Absolute, out var cover)
                ? cover
                : null,
        };
    }

    public async Task<IReadOnlyList<BookMetadata>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var path = new Uri(
            string.Create(
                CultureInfo.InvariantCulture,
                $"search.json?q={Uri.EscapeDataString(query)}"
                + $"&fields={SearchFields}&limit={maxResults}"),
            UriKind.Relative);

        var response = await GetAsync(
                path,
                OpenLibraryJsonContext.Default.OlSearchResponse,
                cancellationToken)
            .ConfigureAwait(false);

        if (response.Docs is not { } docs)
        {
            throw BookHttp.Unexpected(SourceId);
        }

        return
        [
            .. docs
                .Where(w => w.Key is not null && w.Title is not null)
                .Select(w => new BookMetadata
                {
                    Source = SourceId,
                    SourceId = w.Key!,
                    Title = w.Title!,
                    Subtitle = w.Subtitle,
                    Authors = w.AuthorName ?? [],
                    PageCount = w.NumberOfPagesMedian,
                    PublishedYear = w.FirstPublishYear,
                    CoverImageUrl = w.CoverI is { } id ? CoverUrl(id) : null,
                }),
        ];
    }

    public Task<CoverInput> DownloadCoverAsync(
        Uri url,
        CancellationToken cancellationToken = default) =>
            covers.DownloadAsync(url, CoverHosts, SourceId, cancellationToken);

    /// <summary>
    /// default=false: 404 instead of a blank placeholder when the image is
    /// missing.
    /// </summary>
    private static Uri CoverUrl(long id) =>
        new(string.Create(
            CultureInfo.InvariantCulture,
            $"https://covers.openlibrary.org/b/id/{id}-L.jpg?default=false"));

    private async Task<T> GetAsync<T>(
        Uri path,
        JsonTypeInfo<T> type,
        CancellationToken cancellationToken)
    {
        var body = await BookHttp
            .GetJsonAsync(http, path, SourceId, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return JsonSerializer.Deserialize(body, type)
                ?? throw BookHttp.Unexpected(SourceId);
        }
        catch (JsonException ex)
        {
            throw BookHttp.Unexpected(SourceId, ex);
        }
    }

    private static string? First(IReadOnlyList<string>? values) =>
        values is { Count: > 0 } ? values[0] : null;

    private static List<string> Names(IReadOnlyList<OlName>? names) =>
        [.. (names ?? []).Select(n => n.Name).OfType<string>()];
}
