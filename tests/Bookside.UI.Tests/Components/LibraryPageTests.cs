using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.UI.Pages;

namespace Bookside.UI.Tests.Components;

public sealed class LibraryPageTests : ComponentTestBase
{
    [Fact]
    public void Empty_library_invites_adding_a_book()
    {
        var page = Render<LibraryPage>();

        page.WaitForAssertion(() => Assert.Contains("Your library is empty.", page.Markup));
        Assert.NotNull(page.Find("a[href='books/new']"));
    }

    [Fact]
    public async Task Lists_books_with_authors_and_links()
    {
        var book = await AddBookAsync("Dune", "Frank Herbert");
        await AddBookAsync("Emma", "Jane Austen");

        var page = Render<LibraryPage>();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".book-card").Count));
        var card = page.Find($"a.book-card[href='books/{book.Id}']");
        Assert.Contains("Dune", card.TextContent);
        Assert.Contains("Frank Herbert", card.TextContent);
        Assert.Contains("2 books", page.Find(".count").TextContent);
    }

    [Fact]
    public async Task Query_string_filter_shows_only_matching_books()
    {
        var reading = await AddBookAsync("Middlemarch");
        await AddBookAsync("Unopened");
        Success(await Library.AddReadingSessionAsync(reading.Id, new ReadingSessionInput { Status = ReadingStatus.Reading }, Ct));

        Navigation.NavigateTo("books?filter=reading");
        var page = Render<LibraryPage>();

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".book-card")));
        Assert.Contains("Middlemarch", page.Find(".book-card").TextContent);
        Assert.Contains("Reading", page.Find(".book-card .badge").TextContent);
        Assert.Equal("true", page.Find("a.chip.selected").GetAttribute("aria-selected"));
    }

    [Fact]
    public async Task No_matches_offers_to_clear_filters()
    {
        await AddBookAsync("Dune");

        Navigation.NavigateTo("books?q=zzz");
        var page = Render<LibraryPage>();

        page.WaitForAssertion(() => Assert.Contains("No books match.", page.Markup));
        Assert.Equal("zzz", page.Find("#library-search").GetAttribute("value"));
    }

    [Fact]
    public async Task Navigating_back_to_an_earlier_query_reloads_the_list()
    {
        await AddBookAsync("Dune");
        await AddBookAsync("Emma");
        Navigation.NavigateTo("books?q=emma");
        var page = Render<LibraryPage>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".book-card")));

        Navigation.NavigateTo("books");

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".book-card").Count));
        Assert.Equal("", page.Find("#library-search").GetAttribute("value") ?? "");
    }

    [Fact]
    public async Task Typing_a_search_updates_the_url_after_a_pause()
    {
        await AddBookAsync("Dune");
        var page = Render<LibraryPage>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".book-card")));

        page.Find("#library-search").Input("du ne");

        page.WaitForAssertion(() => Assert.EndsWith("/books?q=du%20ne", Navigation.Uri, StringComparison.Ordinal), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Show_more_appends_the_next_page()
    {
        for (var i = 0; i < LibraryPage.PageSize + 2; i++)
        {
            await AddBookAsync($"Book {i:D3}");
        }

        var page = Render<LibraryPage>();
        page.WaitForAssertion(() => Assert.Equal(LibraryPage.PageSize, page.FindAll(".book-card").Count));

        page.Find(".more button").Click();

        page.WaitForAssertion(() => Assert.Equal(LibraryPage.PageSize + 2, page.FindAll(".book-card").Count));
        Assert.Empty(page.FindAll(".more button"));
    }
}
