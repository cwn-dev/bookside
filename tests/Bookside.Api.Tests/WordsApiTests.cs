using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bookside.Core.Books.Services;
using Bookside.Core.Vocabulary.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.Api.Tests;

public sealed class WordsApiTests : IAsyncLifetime
{
    private static readonly Uri WordsUri = new("/api/words", UriKind.Relative);

    private readonly ApiHostFactory _factory = new();
    private HttpClient _client = null!;

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ApiHostFactory.Token);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong")]
    public async Task Missing_or_wrong_token_is_401(string? token)
    {
        _client.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);

        var response = await PostAsync(new { term = "petrichor" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
        Assert.Null(await FindWordAsync("petrichor"));
    }

    [Fact]
    public async Task Api_is_not_mapped_without_a_configured_token()
    {
        await using var factory = new ApiHostFactory(token: null);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "");

        var response = await client.PostAsJsonAsync(WordsUri, new { term = "petrichor" }, Ct);

        // Falls through to the Blazor routes, which reject the POST.
        Assert.False(response.IsSuccessStatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider
            .GetRequiredService<IVocabularyService>()
            .FindWordAsync("petrichor", cancellationToken: Ct));
    }

    [Fact]
    public async Task Adding_a_word_returns_201_with_the_word()
    {
        var response = await PostAsync(new { term = "petrichor", context = "after rain" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await JsonAsync(response);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"/words/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal("NotFound", body.GetProperty("definition").GetProperty("status").GetString());
        Assert.Equal(id, (await FindWordAsync("petrichor"))?.Id);
    }

    [Fact]
    public async Task Adding_an_existing_word_returns_200_with_it()
    {
        var first = await JsonAsync(await PostAsync(new { term = "petrichor" }));

        var response = await PostAsync(new { term = "Petrichor" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            first.GetProperty("id").GetGuid(),
            (await JsonAsync(response)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Book_title_links_the_word_ignoring_case()
    {
        var bookId = await AddBookAsync("Dune");
        await AddBookAsync("Dune Messiah");

        var response = await PostAsync(new { term = "kanly", book = "dune" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(bookId, (await FindWordAsync("kanly"))?.BookId);
    }

    [Theory]
    [InlineData("Ender’s Game", "ender's game")]
    [InlineData("Ender's Game", "Ender’s Game")]
    public async Task Book_title_matches_any_apostrophe(string title, string sent)
    {
        var bookId = await AddBookAsync(title);

        var response = await PostAsync(new { term = "kanly", book = sent });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(bookId, (await FindWordAsync("kanly"))?.BookId);
    }

    [Theory]
    [InlineData("Emma", "No book titled")]
    [InlineData("Dune", "2 books are titled")]
    public async Task Unknown_or_ambiguous_book_title_is_400(string title, string message)
    {
        await AddBookAsync("Dune");
        await AddBookAsync("Dune");

        var response = await PostAsync(new { term = "kanly", book = title });

        await AssertFieldErrorAsync(response, "book", message);
        Assert.Null(await FindWordAsync("kanly"));
    }

    [Fact]
    public async Task Book_and_book_id_together_is_400()
    {
        var bookId = await AddBookAsync("Dune");

        var response = await PostAsync(new { term = "kanly", book = "Dune", bookId });

        await AssertFieldErrorAsync(response, "book", "not both");
    }

    [Fact]
    public async Task Missing_term_is_400_with_a_field_error()
    {
        var response = await PostAsync(new { context = "no term" });

        await AssertFieldErrorAsync(response, "term", "");
    }

    [Fact]
    public async Task Malformed_json_is_400_problem_details_not_the_html_page()
    {
        using var content = new StringContent("{ not json", null, "application/json");

        var response = await _client.PostAsync(WordsUri, content, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    private Task<HttpResponseMessage> PostAsync(object body) =>
        _client.PostAsJsonAsync(WordsUri, body, Ct);

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private static async Task AssertFieldErrorAsync(
        HttpResponseMessage response,
        string field,
        string message)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await JsonAsync(response);
        Assert.Equal("validation", body.GetProperty("code").GetString());
        var errors = body.GetProperty("errors").GetProperty(field);
        Assert.Contains(message, errors[0].GetString(), StringComparison.Ordinal);
    }

    private async Task<Guid> AddBookAsync(string title)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var library = scope.ServiceProvider.GetRequiredService<ILibraryService>();
        var book = await library.AddBookAsync(new BookInput { Title = title }, Ct);
        return book.Value.Id;
    }

    private async Task<WordSummary?> FindWordAsync(string term)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IVocabularyService>()
            .FindWordAsync(term, cancellationToken: Ct);
    }
}
