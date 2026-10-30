using Bookside.Core.Books;
using Bookside.Core.Books.Lookup;
using Bookside.Core.Books.Services;
using Bookside.Core.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.Data.Tests.Services;

public sealed class BookMetadataServiceTests : ServiceTestBase
{
    private readonly FakeBookLookupService _first = new("first");
    private readonly FakeBookLookupService _second = new("second");

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IBookLookupService>(_first).AddSingleton<IBookLookupService>(_second);

    [Fact]
    public async Task An_isbn_10_is_looked_up_as_isbn_13_and_fills_a_missing_isbn()
    {
        _first.Isbns["9780060512750"] = Result("first", "The Dispossessed");

        var results = Success(await Metadata.SearchAsync(" 0-06-051275-X ", Ct));

        var book = Assert.Single(results);
        Assert.Equal(("The Dispossessed", "9780060512750"), (book.Title, book.Isbn13));
        Assert.Empty(_first.Searches);
    }

    [Fact]
    public async Task Every_provider_is_searched_and_results_are_taken_in_turns()
    {
        _first.Results = [Result("first", "A1"), Result("first", "A2"), Result("first", "A3")];
        _second.Results = [Result("second", "B1")];

        var results = Success(await Metadata.SearchAsync("x", Ct));

        Assert.Equal(["A1", "B1", "A2", "A3"], results.Select(r => r.Title));
        Assert.Equal(["first", "second", "first", "first"], results.Select(r => r.Source));
    }

    [Fact]
    public async Task Providers_are_searched_at_the_same_time()
    {
        var gate = new TaskCompletionSource();
        _first.SearchGate = gate.Task;
        _second.Results = [Result("second", "Mort")];

        var search = Metadata.SearchAsync("mort", Ct);

        Assert.Equal(["mort"], _second.Searches);
        Assert.False(search.IsCompleted);
        gate.SetResult();
        Assert.Single(Success(await search));
    }

    [Fact]
    public async Task A_failing_provider_is_skipped_when_another_has_results()
    {
        _first.Unavailable = true;
        _second.Results = [Result("second", "Mort")];

        var results = Success(await Metadata.SearchAsync("mort", Ct));

        Assert.Equal("second", Assert.Single(results).Source);
        Assert.Equal(["mort"], _first.Searches);
    }

    [Fact]
    public async Task No_results_anywhere_is_an_empty_list_but_a_failure_makes_it_unavailable()
    {
        Assert.Empty(Success(await Metadata.SearchAsync("nothing", Ct)));

        _second.Unavailable = true;
        var error = Failure(await Metadata.SearchAsync("nothing", Ct), ErrorType.Unavailable);
        Assert.Equal(ErrorCodes.BookLookupUnavailable, error.Code);
    }

    [Fact]
    public async Task Values_the_book_form_would_reject_are_dropped()
    {
        _first.Results =
        [
            Result("first", "Kept") with
            {
                Description = new string('x', Book.DescriptionMaxLength + 1),
                Authors = ["A", " A ", new string('n', Author.NameMaxLength + 1)],
                Isbn13 = "9780000000001",
                Isbn10 = "0-06-051275-X",
                PageCount = 0,
            },
            Result("first", new string('t', Book.TitleMaxLength + 1)),
        ];

        var book = Assert.Single(Success(await Metadata.SearchAsync("x", Ct)));

        Assert.Equal(["A"], book.Authors);
        Assert.Equal((null, "006051275X", null), (book.Isbn13, book.Isbn10, book.PageCount));
        Assert.Null(book.Description);
    }

    [Fact]
    public async Task Covers_download_through_the_provider_that_found_the_book()
    {
        var book = Result("second", "Mort") with
        {
            CoverImageUrl = new Uri("https://covers.test/1.jpg"),
        };

        var cover = Success(await Metadata.DownloadCoverAsync(book, Ct));

        Assert.Equal("https://covers.test/1.jpg", cover.SourceUrl);
        _second.Unavailable = true;
        Failure(await Metadata.DownloadCoverAsync(book, Ct), ErrorType.Unavailable);
        Failure(
            await Metadata.DownloadCoverAsync(book with { Source = "gone" }, Ct),
            ErrorType.Validation);
    }

    [Fact]
    public async Task Details_come_from_the_provider_that_found_the_book()
    {
        _second.Descriptions["Mort"] = "First.\n\nSecond.";
        var book = Result("second", "Mort") with { Description = "First. Second." };

        var details = Success(await Metadata.GetDetailsAsync(book, Ct));

        Assert.Equal("First.\n\nSecond.", details.Description);
        Failure(
            await Metadata.GetDetailsAsync(book with { Source = "gone" }, Ct),
            ErrorType.Validation);
        _second.Unavailable = true;
        Failure(await Metadata.GetDetailsAsync(book, Ct), ErrorType.Unavailable);
    }

    [Fact]
    public async Task A_details_description_that_is_too_long_keeps_the_search_one()
    {
        _first.Descriptions["Mort"] = new string('x', Book.DescriptionMaxLength + 1);
        var book = Result("first", "Mort") with { Description = "Short." };

        var details = Success(await Metadata.GetDetailsAsync(book, Ct));

        Assert.Equal("Short.", details.Description);
    }

    [Fact]
    public async Task A_blank_query_is_a_validation_error() =>
        Failure(await Metadata.SearchAsync(" ", Ct), ErrorType.Validation);

    private static BookMetadata Result(string source, string title) =>
        new() { Source = source, SourceId = title, Title = title };
}

public sealed class FakeBookLookupService(string source) : IBookLookupService
{
    public string Source => source;

    public Dictionary<string, BookMetadata> Isbns { get; } = [];

    public IReadOnlyList<BookMetadata> Results { get; set; } = [];

    public List<string> Searches { get; } = [];

    /// <summary>
    /// Description returned by <see cref="GetDetailsAsync"/>, by source id.
    /// </summary>
    public Dictionary<string, string> Descriptions { get; } = [];

    public bool Unavailable { get; set; }

    /// <summary>
    /// When set, text searches wait for this task.
    /// </summary>
    public Task? SearchGate { get; set; }

    public Task<BookMetadata?> FindByIsbnAsync(
        string isbn13,
        CancellationToken cancellationToken = default) =>
            Task.FromResult(Fail() ?? Isbns.GetValueOrDefault(isbn13));

    public async Task<IReadOnlyList<BookMetadata>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        Searches.Add(query);
        if (SearchGate is not null)
        {
            await SearchGate.WaitAsync(cancellationToken);
        }

        Fail();
        return Results;
    }

    public Task<BookMetadata> GetDetailsAsync(
        BookMetadata book,
        CancellationToken cancellationToken = default)
    {
        Fail();
        return Task.FromResult(
            Descriptions.TryGetValue(book.SourceId, out var description)
                ? book with { Description = description }
                : book);
    }

    public Task<CoverInput> DownloadCoverAsync(
        Uri url,
        CancellationToken cancellationToken = default)
    {
        Fail();
        return Task.FromResult(new CoverInput
        {
            Content = new byte[] { 1 },
            ContentType = "image/png",
            SourceUrl = url.AbsoluteUri,
        });
    }

    private BookMetadata? Fail() =>
        Unavailable ? throw new BookLookupException($"Simulated {source} outage.") : null;
}
