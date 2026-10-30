using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bookside.Core.Books.Lookup;
using Bookside.Core.Books.Services;
using Microsoft.Extensions.Options;

namespace Bookside.Integrations.Books;

/// <summary>
/// Google Books client; needs an API key (keyless calls are rate-limited to
/// nothing).
/// </summary>
internal sealed class GoogleBooksClient : IBookLookupService
{
    public const string SourceId = "googlebooks";

    private static readonly IReadOnlySet<string> CoverHosts = new HashSet<string>(
        ["books.google.com", "books.googleusercontent.com"],
        StringComparer.OrdinalIgnoreCase);

    private readonly HttpClient _http;
    private readonly BookCoverDownloader _covers;

    public GoogleBooksClient(
        HttpClient http,
        BookCoverDownloader covers,
        IOptions<GoogleBooksOptions> options)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);

        // Typed clients get their own HttpClient instance, so its default
        // headers are ours to set.
        _http = http;
        _covers = covers;
        if (options.Value.ApiKey is { Length: > 0 } key)
        {
            _http.DefaultRequestHeaders.Add("X-Goog-Api-Key", key);
        }
    }

    public string Source => SourceId;

    public async Task<BookMetadata?> FindByIsbnAsync(
        string isbn13,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn13);

        var results = await QueryAsync($"isbn:{isbn13}", 1, cancellationToken)
            .ConfigureAwait(false);

        return results.Count > 0 ? results[0] : null;
    }

    public Task<IReadOnlyList<BookMetadata>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        return QueryAsync(query, maxResults, cancellationToken);
    }

    public async Task<BookMetadata> GetDetailsAsync(
        BookMetadata book,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);

        var path = new Uri(
            $"volumes/{Uri.EscapeDataString(book.SourceId)}",
            UriKind.Relative);

        var volume = await GetAsync(
                path,
                GoogleBooksJsonContext.Default.GbVolume,
                cancellationToken)
            .ConfigureAwait(false);

        return BookText.FromHtml(volume.VolumeInfo?.Description) is { } description
            ? book with { Description = description }
            : book;
    }

    public Task<CoverInput> DownloadCoverAsync(
        Uri url,
        CancellationToken cancellationToken = default) =>
            _covers.DownloadAsync(url, CoverHosts, SourceId, cancellationToken);

    private async Task<IReadOnlyList<BookMetadata>> QueryAsync(
        string q,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var path = new Uri(
            string.Create(
                CultureInfo.InvariantCulture,
                $"volumes?q={Uri.EscapeDataString(q)}&maxResults={maxResults}&printType=books"),
            UriKind.Relative);

        var response = await GetAsync(
                path,
                GoogleBooksJsonContext.Default.GbResponse,
                cancellationToken)
            .ConfigureAwait(false);

        if (response.Kind != "books#volumes")
        {
            throw BookHttp.Unexpected(SourceId);
        }

        return
        [
            .. (response.Items ?? [])
                .Where(v => v.Id is not null && v.VolumeInfo?.Title is not null)
                .Select(v => Map(v.Id!, v.VolumeInfo!)),
        ];
    }

    private async Task<T> GetAsync<T>(
        Uri path,
        JsonTypeInfo<T> type,
        CancellationToken cancellationToken)
    {
        var body = await BookHttp
            .GetJsonAsync(_http, path, SourceId, cancellationToken)
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

    private static BookMetadata Map(string id, GbVolumeInfo info) => new()
    {
        Source = SourceId,
        SourceId = id,
        Title = info.Title!,
        Subtitle = info.Subtitle,
        Authors = info.Authors ?? [],
        PageCount = info.PageCount,
        Isbn13 = Identifier(info, "ISBN_13"),
        Isbn10 = Identifier(info, "ISBN_10"),
        Publisher = info.Publisher,
        PublishedYear = BookHttp.Year(info.PublishedDate),
        Description = info.Description,
        CoverImageUrl = Thumbnail(info.ImageLinks?.Thumbnail),
    };

    private static string? Identifier(GbVolumeInfo info, string type) =>
        info.IndustryIdentifiers?.FirstOrDefault(i => i.Type == type)?.Identifier;

    /// <summary>
    /// Google returns http:// thumbnail URLs; the same URL works over https.
    /// </summary>
    private static Uri? Thumbnail(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri
            : null;
}
