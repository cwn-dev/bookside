using Bookside.UI.Pages;

namespace Bookside.UI.Tests.Components;

public sealed class WordDetailsPageTests : ComponentTestBase
{
    [Fact]
    public async Task Shows_definition_pronunciation_and_where_it_was_found()
    {
        Dictionary.Define("ennui", "Listlessness.", example: "A wave of ennui.");
        var book = await AddBookAsync("Mort");
        var word = await AddWordAsync("Ennui", book.Id, context: "Ennui set in.", page: 42);

        var page = Render<WordDetailsPage>(p => p.Add(x => x.Id, word.Id));

        page.WaitForAssertion(() => Assert.Equal("Ennui", page.Find("h1").TextContent));
        Assert.Equal("/test/", page.Find(".phonetic").TextContent);
        Assert.Equal("https://audio.example/ennui.mp3", page.Find("audio").GetAttribute("src"));
        Assert.Equal("noun", page.Find(".part-of-speech").TextContent);
        Assert.Contains("Listlessness.", page.Find(".meaning li").TextContent);
        Assert.Contains("A wave of ennui.", page.Find(".meaning .example").TextContent);
        Assert.Equal($"books/{book.Id}", page.Find(".found-in a").GetAttribute("href"));
        Assert.Contains("page 42", page.Find(".found-in").TextContent);
        Assert.Equal("Ennui set in.", page.Find(".context").TextContent);
    }

    [Fact]
    public void Unknown_word_shows_not_found()
    {
        var page = Render<WordDetailsPage>(p => p.Add(x => x.Id, Guid.NewGuid()));

        page.WaitForAssertion(() => Assert.Equal("Word not found", page.Find("h1").TextContent));
    }

    [Fact]
    public async Task Retry_definition_reports_an_outage_then_succeeds()
    {
        Dictionary.Unavailable = true;
        var word = await AddWordAsync("petrichor");
        var page = Render<WordDetailsPage>(p => p.Add(x => x.Id, word.Id));
        page.WaitForAssertion(() => page.Find(".definition-status"));

        page.FindAll("button").Single(b => b.TextContent.Trim() == "Retry definition").Click();
        page.WaitForAssertion(() => Assert.Contains("dictionary is unavailable", page.Find(".alert-error").TextContent));

        Dictionary.Unavailable = false;
        Dictionary.Define("petrichor", "The smell of rain on dry earth.");
        page.FindAll("button").Single(b => b.TextContent.Trim() == "Retry definition").Click();

        page.WaitForAssertion(() => Assert.Contains("The smell of rain", page.Find(".meaning li").TextContent));
        Assert.Empty(page.FindAll(".alert-error"));
    }

    [Fact]
    public async Task Not_found_can_be_checked_again()
    {
        var word = await AddWordAsync("brillig");
        var page = Render<WordDetailsPage>(p => p.Add(x => x.Id, word.Id));
        page.WaitForAssertion(() => Assert.Contains("no entry for this word", page.Find(".definition-status").TextContent));

        Dictionary.Define("brillig", "Four o'clock in the afternoon.");
        page.FindAll("button").Single(b => b.TextContent.Trim() == "Check again").Click();

        page.WaitForAssertion(() => Assert.Contains("Four o'clock", page.Find(".meaning li").TextContent));
    }

    [Fact]
    public async Task Edit_picks_a_book_with_the_type_ahead_and_saves()
    {
        await AddBookAsync("Dune", "Frank Herbert");
        await AddBookAsync("Emma");
        var word = await AddWordAsync("kanly");
        var page = Render<WordDetailsPage>(p => p.Add(x => x.Id, word.Id));
        page.WaitForAssertion(() => page.Find("h1"));

        page.FindAll("button").Single(b => b.TextContent == "Edit").Click();
        page.Find(".book-picker input").Input("dun");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".book-picker .results button")), TimeSpan.FromSeconds(3));
        Assert.Contains("Frank Herbert", page.Find(".book-picker .results button").TextContent);
        page.Find(".book-picker .results button").Click();
        page.WaitForAssertion(() => Assert.Equal("Dune", page.Find(".book-picker .selected-title").TextContent));
        page.Find(".editor textarea").Change("Kanly was declared.");
        page.Find(".editor form").Submit();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".editor")));
        Assert.Equal("Dune", page.Find(".found-in a").TextContent);
        Assert.Equal("Kanly was declared.", page.Find(".context").TextContent);
    }

    [Fact]
    public async Task Renaming_onto_an_existing_word_links_to_it()
    {
        var existing = await AddWordAsync("ennui");
        var word = await AddWordAsync("enui");
        var page = Render<WordDetailsPage>(p => p.Add(x => x.Id, word.Id));
        page.WaitForAssertion(() => page.Find("h1"));

        page.FindAll("button").Single(b => b.TextContent == "Edit").Click();
        page.Find(".editor input[id$='-term']").Change("Ennui");
        page.Find(".editor form").Submit();

        page.WaitForAssertion(() => Assert.Equal($"words/{existing.Id}", page.Find(".editor .alert-error a").GetAttribute("href")));
    }

    [Fact]
    public async Task Delete_returns_to_the_list()
    {
        var word = await AddWordAsync("ennui");
        var page = Render<WordDetailsPage>(p => p.Add(x => x.Id, word.Id));
        page.WaitForAssertion(() => page.Find("h1"));

        page.FindAll("button").Single(b => b.TextContent == "Delete word").Click();
        page.Find(".confirm .btn-solid").Click();

        page.WaitForAssertion(() => Assert.EndsWith("/words", Navigation.Uri, StringComparison.Ordinal));
        Assert.False((await Vocabulary.GetWordAsync(word.Id, Ct)).IsSuccess);
    }
}
