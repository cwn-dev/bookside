using System.Net;
using Bookside.Core.Books.Lookup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Bookside.Integrations.Tests;

/// <summary>
/// Both book clients through the production registration, each with its own
/// stubbed primary handler (covers go through the shared cover client).
/// </summary>
public sealed class BookLookupClientTests : IDisposable
{
    private const string CoverClient = IntegrationsServiceCollectionExtensions.BookCoverClientName;

    private readonly StubHttpMessageHandler _openLibrary = new();
    private readonly StubHttpMessageHandler _google = new();
    private readonly StubHttpMessageHandler _covers = new();
    private readonly ServiceProvider _services;

    public BookLookupClientTests()
    {
        var services = new ServiceCollection();
        var ol = services
            .AddOpenLibraryClient()
            .ConfigurePrimaryHttpMessageHandler(() => _openLibrary);
        var gb = services
            .AddGoogleBooksClient(o => o.ApiKey = "secret")
            .ConfigurePrimaryHttpMessageHandler(() => _google);
        services
            .AddHttpClient(CoverClient)
            .ConfigurePrimaryHttpMessageHandler(() => _covers);

        foreach (var name in new[] { ol.Name, gb.Name, CoverClient })
        {
            services.Configure<HttpStandardResilienceOptions>(
                $"{name}-standard",
                o => o.Retry.Delay = TimeSpan.Zero);
        }

        _services = services.BuildServiceProvider();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IBookLookupService OpenLibrary => Provider("openlibrary");

    private IBookLookupService Google => Provider("googlebooks");

    public void Dispose() => _services.Dispose();

    [Fact]
    public async Task Open_Library_isbn_lookup_maps_the_edition()
    {
        _openLibrary.Reply(HttpStatusCode.OK, """
            {"ISBN:9780552166591": {
              "key": "/books/OL28477029M", "title": "The Colour of Magic",
              "authors": [{"name": "Terry Pratchett"}], "number_of_pages": 287,
              "identifiers": {"isbn_13": ["9780552166591"], "openlibrary": ["OL28477029M"]},
              "publishers": [{"name": "Corgi Books"}], "publish_date": "May 1985",
              "cover": {"large": "https://covers.openlibrary.org/b/id/14647238-L.jpg"}}}
            """);

        var book = await OpenLibrary.FindByIsbnAsync("9780552166591", Ct);

        Assert.NotNull(book);
        Assert.Equal("/books/OL28477029M", book.SourceId);
        Assert.Equal("The Colour of Magic", book.Title);
        Assert.Equal((287, 1985), (book.PageCount, book.PublishedYear));
        Assert.Equal(["Terry Pratchett"], book.Authors);
        Assert.Equal(("9780552166591", null), (book.Isbn13, book.Isbn10));
        Assert.Equal("Corgi Books", book.Publisher);
        Assert.Equal(
            "https://covers.openlibrary.org/b/id/14647238-L.jpg",
            book.CoverImageUrl?.AbsoluteUri);
        Assert.Equal(
            "https://openlibrary.org/api/books?bibkeys=ISBN:9780552166591&jscmd=data&format=json",
            Assert.Single(_openLibrary.Requests).AbsoluteUri);
    }

    [Fact]
    public async Task Open_Library_unknown_isbn_is_null()
    {
        _openLibrary.Reply(HttpStatusCode.OK, "{}");

        Assert.Null(await OpenLibrary.FindByIsbnAsync("9780000000002", Ct));
    }

    [Fact]
    public async Task Open_Library_search_maps_works_with_cover_urls()
    {
        _openLibrary.Reply(HttpStatusCode.OK, """
            {"numFound": 2, "docs": [
              {"key": "/works/OL453658W", "title": "Mort", "author_name": ["Terry Pratchett"],
               "first_publish_year": 1987, "cover_i": 14648805, "number_of_pages_median": 297},
              {"key": "/works/OL1W", "title": "No cover"}]}
            """);

        var books = await OpenLibrary.SearchAsync("mort pratchett", 10, Ct);

        Assert.Equal(["Mort", "No cover"], books.Select(b => b.Title));
        Assert.Equal(
            "https://covers.openlibrary.org/b/id/14648805-L.jpg?default=false",
            books[0].CoverImageUrl?.AbsoluteUri);
        Assert.Equal((297, 1987), (books[0].PageCount, books[0].PublishedYear));
        Assert.Null(books[1].CoverImageUrl);
        Assert.StartsWith(
            "https://openlibrary.org/search.json?q=mort%20pratchett&fields=",
            Assert.Single(_openLibrary.Requests).AbsoluteUri,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "<html>maintenance</html>")]
    [InlineData(HttpStatusCode.OK, """{"error": "x"}""")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
    public async Task Open_Library_errors_and_unexpected_replies_throw(
        HttpStatusCode status,
        string body)
    {
        _openLibrary.Reply(status, body);

        await Assert.ThrowsAsync<BookLookupException>(
            () => OpenLibrary.SearchAsync("x", 10, Ct));
    }

    [Fact]
    public async Task Covers_download_from_the_providers_own_hosts_only_through_the_cover_client()
    {
        _covers.Reply((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3])
            {
                Headers = { ContentType = new("image/jpeg") },
            },
        }));
        const string url = "https://covers.openlibrary.org/b/id/1-L.jpg?default=false";

        var cover = await OpenLibrary.DownloadCoverAsync(new Uri(url), Ct);

        Assert.Equal(("image/jpeg", 3), (cover.ContentType, cover.Content.Length));
        Assert.Equal(url, cover.SourceUrl);
        await Assert.ThrowsAsync<BookLookupException>(
            () => OpenLibrary.DownloadCoverAsync(new Uri("https://169.254.169.254/latest"), Ct));
        await Assert.ThrowsAsync<BookLookupException>(
            () => OpenLibrary.DownloadCoverAsync(
                new Uri("http://covers.openlibrary.org/b/id/1-L.jpg"),
                Ct));
        Assert.Single(_covers.Requests);
    }

    [Fact]
    public async Task A_cover_that_is_not_an_image_throws()
    {
        _covers.Reply(HttpStatusCode.NotFound, "not found", "text/plain");

        await Assert.ThrowsAsync<BookLookupException>(
            () => OpenLibrary.DownloadCoverAsync(
                new Uri("https://covers.openlibrary.org/b/id/1-L.jpg"),
                Ct));
    }

    [Fact]
    public async Task Google_isbn_lookup_sends_the_key_as_a_header_and_maps_the_volume()
    {
        HttpRequestMessage? request = null;
        _google.Reply((r, _) =>
        {
            request = r;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"kind": "books#volumes", "totalItems": 1, "items": [{
                      "id": "abc123", "volumeInfo": {
                        "title": "Mort", "authors": ["Terry Pratchett"], "publisher": "Corgi",
                        "publishedDate": "1988-11-01", "pageCount": 288,
                        "description": "Death takes an apprentice.",
                        "industryIdentifiers": [
                          {"type": "ISBN_10", "identifier": "0552131067"},
                          {"type": "ISBN_13", "identifier": "9780552131063"}],
                        "imageLinks": {
                          "thumbnail": "http://books.google.com/books/content?id=abc123&img=1"}}}]}
                    """),
            });
        });

        var book = await Google.FindByIsbnAsync("9780552131063", Ct);

        Assert.NotNull(book);
        Assert.Equal(("abc123", "Mort", "Corgi"), (book.SourceId, book.Title, book.Publisher));
        Assert.Equal((1988, 288), (book.PublishedYear, book.PageCount));
        Assert.Equal(("9780552131063", "0552131067"), (book.Isbn13, book.Isbn10));
        Assert.Equal("Death takes an apprentice.", book.Description);
        Assert.Equal(
            "https://books.google.com/books/content?id=abc123&img=1",
            book.CoverImageUrl?.AbsoluteUri);
        Assert.Equal(["secret"], request!.Headers.GetValues("X-Goog-Api-Key"));
        Assert.Equal(
            "https://www.googleapis.com/books/v1/volumes"
            + "?q=isbn%3A9780552131063&maxResults=1&printType=books",
            request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Google_no_matches_is_null_and_a_quota_error_throws()
    {
        _google.Reply(HttpStatusCode.OK, """{"kind": "books#volumes", "totalItems": 0}""");
        Assert.Null(await Google.FindByIsbnAsync("9780000000002", Ct));

        _google.Reply(HttpStatusCode.TooManyRequests, """{"error": {"code": 429}}""");
        await Assert.ThrowsAsync<BookLookupException>(() => Google.SearchAsync("x", 10, Ct));
    }

    [Fact]
    public async Task Google_details_turn_the_volume_html_into_paragraphs()
    {
        _google.Reply(HttpStatusCode.OK, """
            {"id": "abc123", "volumeInfo": {"title": "The Dispossessed", "description":
              "<p>Centuries ago, the moon&nbsp;Anarres was settled.</p> <p><i>The Dispossessed</i> is a<br>penetrating examination &amp; more.</p><ul><li>One</li><li>Two</li></ul>"}}
            """);
        var book = new BookMetadata
        {
            Source = "googlebooks",
            SourceId = "abc 123",
            Title = "The Dispossessed",
            Description = "Flattened.",
        };

        var details = await Google.GetDetailsAsync(book, Ct);

        Assert.Equal(
            "Centuries ago, the moon Anarres was settled.\n\n"
            + "The Dispossessed is a\npenetrating examination & more.\n\n• One\n• Two",
            details.Description);
        Assert.Equal(
            "https://www.googleapis.com/books/v1/volumes/abc%20123",
            Assert.Single(_google.Requests).AbsoluteUri);
    }

    [Fact]
    public async Task Open_Library_details_fall_back_from_the_edition_to_its_work()
    {
        _openLibrary
            .Reply(HttpStatusCode.OK, """{"works": [{"key": "/works/OL59863W"}]}""")
            .Reply(HttpStatusCode.OK, """
                {"description": {"type": "/type/text", "value":
                  "**From the *Times* author.**\r\n\r\nSee [the site](https://x.test) or [this][1].\r\n\r\n[1]: https://y.test\r\n\r\n----------\r\nAlso contained in:\r\n- Omnibus"}}
                """);
        var book = new BookMetadata
        {
            Source = "openlibrary",
            SourceId = "/books/OL28477029M",
            Title = "The Dispossessed",
        };

        var details = await OpenLibrary.GetDetailsAsync(book, Ct);

        Assert.Equal(
            "From the Times author.\n\nSee the site or this.",
            details.Description);
        Assert.Equal(
            [
                "https://openlibrary.org/books/OL28477029M.json",
                "https://openlibrary.org/works/OL59863W.json",
            ],
            _openLibrary.Requests.Select(u => u.AbsoluteUri));
    }

    [Fact]
    public async Task Open_Library_details_use_a_plain_string_and_ignore_foreign_keys()
    {
        _openLibrary.Reply(HttpStatusCode.OK, """{"description": "One.\r\n\r\nTwo."}""");
        var book = new BookMetadata
        {
            Source = "openlibrary",
            SourceId = "/works/OL1W",
            Title = "T",
        };

        Assert.Equal("One.\n\nTwo.", (await OpenLibrary.GetDetailsAsync(book, Ct)).Description);

        var foreign = book with { SourceId = "/../admin" };
        Assert.Same(foreign, await OpenLibrary.GetDetailsAsync(foreign, Ct));
        Assert.Single(_openLibrary.Requests);
    }

    private IBookLookupService Provider(string source) =>
        _services.GetServices<IBookLookupService>().Single(s => s.Source == source);
}
