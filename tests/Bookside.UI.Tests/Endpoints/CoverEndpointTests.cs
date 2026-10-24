using System.Net;
using System.Net.Http.Headers;
using Bookside.Core.Books.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.UI.Tests.Endpoints;

public sealed class CoverEndpointTests : IAsyncLifetime
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private readonly WebHostFactory _factory = new();
    private HttpClient _client = null!;

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public void Host_uses_the_configured_database_path()
    {
        Assert.True(File.Exists(_factory.DatabasePath));
    }

    [Fact]
    public async Task Library_page_is_served()
    {
        var response = await _client.GetAsync(new Uri("/books", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("blazor.web", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_cover_is_404()
    {
        var bookId = await AddBookAsync(withCover: false);

        var response = await _client.GetAsync(CoverUri(bookId), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Versioned_url_is_cacheable_forever_with_hash_etag()
    {
        var (bookId, sha) = await AddBookWithCoverAsync();

        var response = await _client.GetAsync(CoverUri(bookId, sha), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync(Ct));
        Assert.Equal($"\"{sha}\"", response.Headers.ETag?.Tag);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl?.MaxAge);
        Assert.Contains("immutable", response.Headers.CacheControl!.Extensions.Select(e => e.Name));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("stale")]
    public async Task Unversioned_or_stale_url_must_revalidate(string? version)
    {
        var (bookId, _) = await AddBookWithCoverAsync();

        var response = await _client.GetAsync(CoverUri(bookId, version), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoCache);
        Assert.Null(response.Headers.CacheControl?.MaxAge);
    }

    [Fact]
    public async Task Matching_if_none_match_returns_304()
    {
        var (bookId, sha) = await AddBookWithCoverAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, CoverUri(bookId));
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{sha}\""));

        var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(Ct));
    }

    private static Uri CoverUri(Guid bookId, string? version = null) =>
        new(version is null ? $"/covers/{bookId}" : $"/covers/{bookId}?v={version}", UriKind.Relative);

    private async Task<(Guid BookId, string Sha256)> AddBookWithCoverAsync()
    {
        var bookId = await AddBookAsync(withCover: true);
        await using var scope = _factory.Services.CreateAsyncScope();
        var library = scope.ServiceProvider.GetRequiredService<ILibraryService>();
        var book = await library.GetBookAsync(bookId, Ct);
        return (bookId, book.Value.CoverSha256!);
    }

    private async Task<Guid> AddBookAsync(bool withCover)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var library = scope.ServiceProvider.GetRequiredService<ILibraryService>();
        var book = await library.AddBookAsync(new BookInput { Title = "Dune" }, Ct);
        Assert.True(book.IsSuccess);
        if (withCover)
        {
            Assert.True((await library.SetCoverAsync(book.Value.Id, new CoverInput { Content = Png, ContentType = "image/png" }, Ct)).IsSuccess);
        }

        return book.Value.Id;
    }
}
