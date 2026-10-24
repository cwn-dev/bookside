using Bookside.UI.Pages;

namespace Bookside.UI.Tests.Components;

public sealed class VocabularyPageTests : ComponentTestBase
{
    [Fact]
    public void Empty_list_invites_adding_a_word()
    {
        var page = Render<VocabularyPage>();

        page.WaitForAssertion(() => Assert.Contains("No words yet.", page.Markup));
        Assert.NotNull(page.Find(".quick-add input"));
    }

    [Fact]
    public async Task Lists_words_with_definition_status_and_book_link()
    {
        Dictionary.Define("ennui", "Listlessness.");
        var book = await AddBookAsync("Mort");
        var ennui = await AddWordAsync("Ennui", book.Id);
        await AddWordAsync("zzyzx");
        Dictionary.Unavailable = true;
        await AddWordAsync("petrichor");

        var page = Render<VocabularyPage>();

        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll(".word-row").Count));
        var row = Row(page, "Ennui");
        Assert.Equal($"words/{ennui.Id}", row.QuerySelector("a.term")!.GetAttribute("href"));
        Assert.Contains("Listlessness.", row.TextContent);
        Assert.Equal($"http://localhost/words?book={book.Id}", row.QuerySelector("a.book")!.GetAttribute("href"));
        Assert.Contains("Not in dictionary", Row(page, "zzyzx").TextContent);
        Assert.Contains("No definition yet", Row(page, "petrichor").TextContent);
        Assert.Contains("3 words", page.Find(".count").TextContent);
    }

    [Fact]
    public async Task Quick_add_saves_the_word_shows_its_definition_and_clears_the_box()
    {
        Dictionary.Define("ennui", "Listlessness.");
        var page = Render<VocabularyPage>();
        page.WaitForAssertion(() => page.Find(".empty"));

        page.Find(".quick-add input").Change("Ennui");
        page.Find(".quick-add form").Submit();

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".word-row")));
        var notice = page.Find(".word-added");
        Assert.Contains("Listlessness.", notice.TextContent);
        Assert.Equal("Ennui", notice.QuerySelector("a")!.TextContent);
        Assert.Equal("", page.Find(".quick-add input").GetAttribute("value") ?? "");
    }

    [Fact]
    public void Quick_add_during_a_dictionary_outage_still_saves_the_word()
    {
        Dictionary.Unavailable = true;
        var page = Render<VocabularyPage>();
        page.WaitForAssertion(() => page.Find(".empty"));

        page.Find(".quick-add input").Change("petrichor");
        page.Find(".quick-add form").Submit();

        page.WaitForAssertion(() => Assert.Contains("dictionary is unavailable", page.Find(".word-added").TextContent));
        Assert.Single(page.FindAll(".word-row"));
    }

    [Fact]
    public async Task Adding_a_duplicate_links_to_the_existing_word()
    {
        var book = await AddBookAsync("Mort");
        var existing = await AddWordAsync("Ennui", book.Id);
        var page = Render<VocabularyPage>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".word-row")));

        page.Find(".quick-add input").Change(" ENNUI ");
        page.Find(".quick-add form").Submit();

        page.WaitForAssertion(() => Assert.Contains("already on your list (from Mort)", page.Find(".quick-add .alert-error").TextContent));
        Assert.Equal($"words/{existing.Id}", page.Find(".quick-add .alert-error a").GetAttribute("href"));
    }

    [Fact]
    public async Task Book_filter_shows_the_book_and_adds_new_words_to_it()
    {
        var mort = await AddBookAsync("Mort");
        await AddWordAsync("ennui", mort.Id);
        await AddWordAsync("petrichor");

        Navigation.NavigateTo($"words?book={mort.Id}");
        var page = Render<VocabularyPage>();

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".word-row")));
        Assert.Contains("Mort", page.Find(".book-filter").TextContent);
        Assert.Empty(page.FindAll(".word-row a.book")); // redundant when filtered

        page.Find(".quick-add input[id$='-term']").Change("susurrus");
        page.Find(".quick-add input[id$='-page']").Change("12");
        page.Find(".quick-add textarea").Change("A susurrus of leaves.");
        page.Find(".quick-add form").Submit();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".word-row").Count));
        var added = await Vocabulary.FindWordAsync("susurrus", cancellationToken: Ct);
        var details = Success(await Vocabulary.GetWordAsync(added!.Id, Ct));
        Assert.Equal(mort.Id, details.BookId);
        Assert.Equal(12, details.PageNumber);
        Assert.Equal("A susurrus of leaves.", details.Context);
    }

    [Fact]
    public async Task Typing_a_search_filters_via_the_url()
    {
        await AddWordAsync("ennui");
        await AddWordAsync("petrichor");
        var page = Render<VocabularyPage>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".word-row").Count));

        page.Find("#word-search").Input("PETRI");

        page.WaitForAssertion(() => Assert.EndsWith("/words?q=PETRI", Navigation.Uri, StringComparison.Ordinal), TimeSpan.FromSeconds(3));
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".word-row")));
    }

    private static AngleSharp.Dom.IElement Row(IRenderedComponent<VocabularyPage> page, string term) =>
        page.FindAll(".word-row").Single(r => r.QuerySelector("a.term")!.TextContent == term);
}
