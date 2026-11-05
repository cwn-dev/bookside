using Bookside.Core.Books.Services;
using Bookside.Core.Search.Services;
using Bookside.UI.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.UI.Tests.Components;

public sealed class GlobalSearchTests : ComponentTestBase
{
    [Fact]
    public async Task Typing_shows_books_words_tags_and_comments_after_the_pause()
    {
        var mort = await AddBookAsync("Mort", "Terry Pratchett");
        Success(await Tags.SetBookTagsAsync(mort.Id, ["Mortality"], Ct));
        Success(await Library.AddCommentAsync(
            mort.Id,
            new BookCommentInput { Text = "@[Death] is mortal-ish." },
            Ct));
        Dictionary.Define("mortify", "To embarrass.");
        await AddWordAsync("mortify", mort.Id);
        var search = Render<GlobalSearch>();

        search.Find(".search-input").Input("mort");

        search.WaitForAssertion(() =>
            Assert.Equal(
                ["Books", "Comments", "Words", "Tags"], // two columns
                search.FindAll(".search-section h2").Select(h => h.TextContent)));
        Assert.Contains("has-panel", search.Find(".global-search").ClassList);
        Assert.Equal($"books/{mort.Id}", search.Find(".book-hit").GetAttribute("href"));
        Assert.Equal("Mort", search.Find(".book-hit mark").TextContent);
        Assert.Contains(
            "To embarrass.",
            search.Find(".word-hit").TextContent,
            StringComparison.Ordinal);
        Assert.Equal("1", search.Find(".tag-count").TextContent);
        Assert.Contains(
            "books?tag=Mortality",
            search.Find(".tag-hit").GetAttribute("href"),
            StringComparison.Ordinal);
        Assert.Equal("@Death is mortal-ish.", search.Find(".hit-snippet").TextContent);
        Assert.Matches(
            @"^\d{1,2} \w{3} \d{4}, \d{2}:\d{2}$",
            search.Find(".comment-hit time").TextContent.Trim());
        Assert.Empty(search.FindAll(".search-see-all"));
        Assert.Equal("4 results", search.Find("[aria-live]").TextContent);
    }

    [Fact]
    public async Task No_matches_says_so()
    {
        await AddBookAsync("Mort");
        var search = Render<GlobalSearch>();

        search.Find(".search-input").Input("zzz");

        search.WaitForAssertion(() =>
            Assert.Equal("No results for “zzz”.", search.Find(".search-empty").TextContent));
    }

    [Fact]
    public async Task More_books_than_shown_links_to_the_library_search()
    {
        for (var i = 0; i < 7; i++)
        {
            await AddBookAsync($"Book {i}");
        }

        var search = Render<GlobalSearch>();

        search.Find(".search-input").Input("book");

        search.WaitForAssertion(() =>
            Assert.EndsWith(
                "books?q=book",
                search.Find(".search-see-all").GetAttribute("href"),
                StringComparison.Ordinal));
        Assert.Equal(6, search.FindAll(".book-hit").Count);
    }

    [Fact]
    public async Task Escape_clears_and_closes_and_typing_reopens()
    {
        await AddBookAsync("Mort");
        var search = Render<GlobalSearch>();
        search.Find(".search-input").Input("mor");
        search.WaitForAssertion(() => search.Find(".book-hit"));

        search.Find(".search-input").KeyDown("Escape");
        search.WaitForAssertion(() =>
        {
            Assert.Contains("dismissed", search.Find(".global-search").ClassList);
            Assert.Equal("", search.Find(".search-input").GetAttribute("value") ?? "");
            Assert.Empty(search.FindAll(".book-hit"));
        });

        search.Find(".search-input").Input("mort");
        search.WaitForAssertion(() =>
            Assert.DoesNotContain("dismissed", search.Find(".global-search").ClassList));
    }

    [Fact]
    public async Task The_clear_button_empties_the_box_and_closes_the_results()
    {
        await AddBookAsync("Mort");
        var search = Render<GlobalSearch>();
        search.Find(".search-input").Input("mort");
        search.WaitForAssertion(() => search.Find(".book-hit"));
        Assert.Contains("has-query", search.Find(".global-search").ClassList);

        search.Find(".search-clear").Click();

        search.WaitForAssertion(() =>
        {
            var root = search.Find(".global-search").ClassList;
            Assert.Contains("dismissed", root);
            Assert.DoesNotContain("has-query", root);
            Assert.Empty(search.FindAll(".book-hit"));
            Assert.Equal("", search.Find(".search-input").GetAttribute("value") ?? "");
        });
    }

    [Fact]
    public async Task Clicking_the_backdrop_clears_the_query()
    {
        await AddBookAsync("Mort");
        var search = Render<GlobalSearch>();
        search.Find(".search-input").Input("mort");
        search.WaitForAssertion(() => search.Find(".book-hit"));

        search.Find(".search-backdrop").Click();

        search.WaitForAssertion(() =>
        {
            Assert.Contains("dismissed", search.Find(".global-search").ClassList);
            Assert.Equal("", search.Find(".search-input").GetAttribute("value") ?? "");
        });
    }

    [Fact]
    public async Task Enter_opens_the_first_result_and_navigating_clears_the_search()
    {
        var mort = await AddBookAsync("Mort");
        var search = Render<GlobalSearch>();
        search.Find(".search-input").Input("mort");
        search.WaitForAssertion(() => search.Find(".book-hit"));

        search.Find(".search-input").KeyDown("Enter");

        search.WaitForAssertion(() =>
        {
            Assert.EndsWith($"books/{mort.Id}", Navigation.Uri, StringComparison.Ordinal);
            Assert.Contains("dismissed", search.Find(".global-search").ClassList);
            Assert.Empty(search.FindAll(".book-hit"));
            Assert.Equal("", search.Find(".search-input").GetAttribute("value") ?? "");
        });
    }
}

/// <summary>
/// The search service throws; registered before the base initializes the
/// database, which fixes the service provider.
/// </summary>
public sealed class GlobalSearchFailureTests : ComponentTestBase
{
    public GlobalSearchFailureTests() =>
        Services.AddSingleton<ISearchService, ThrowingSearchService>();

    [Fact]
    public async Task Fewer_than_three_characters_never_call_the_service()
    {
        var search = Render<GlobalSearch>();

        search.Find(".search-input").Input(" mo ");
        await Task.Delay(GlobalSearch.SearchDelay * 3, Ct);

        Assert.Empty(search.FindAll(".search-failed"));
        Assert.DoesNotContain("has-panel", search.Find(".global-search").ClassList);
    }

    [Fact]
    public void A_failing_search_shows_an_error_in_the_box_instead_of_throwing()
    {
        var search = Render<GlobalSearch>();

        search.Find(".search-input").Input("mort");

        search.WaitForAssertion(() =>
            Assert.Equal("Search failed.", search.Find(".search-failed").TextContent));
        Assert.Equal("Search failed", search.Find("[aria-live]").TextContent);

        search.Find(".search-input").Input("");
        search.WaitForAssertion(() => Assert.Empty(search.FindAll(".search-failed")));
    }

    private sealed class ThrowingSearchService : ISearchService
    {
        public Task<SearchResults> SearchAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Database is locked.");
    }
}
