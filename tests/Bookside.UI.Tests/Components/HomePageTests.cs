using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.UI.Pages;

namespace Bookside.UI.Tests.Components;

public sealed class HomePageTests : ComponentTestBase
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);

    [Fact]
    public void An_empty_library_shows_the_welcome_card()
    {
        var page = Render<HomePage>();

        page.WaitForAssertion(() => Assert.Equal("Welcome to Bookside", page.Find("h1").TextContent));
        Assert.Contains("Add your first book", page.Find(".welcome").TextContent);
        Assert.Empty(page.FindAll(".stats"));
    }

    [Fact]
    public void Adding_the_first_word_switches_to_the_dashboard()
    {
        Dictionary.Define("ennui", "Listlessness.");
        var page = Render<HomePage>();
        page.WaitForAssertion(() => page.Find(".welcome"));

        page.Find(".welcome input").Change("ennui");
        page.Find(".welcome form").Submit();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".welcome")));
        Assert.Contains("Added ennui", page.Find(".quick-add .word-added").TextContent);
        Assert.Equal("ennui", page.Find(".recent-term").TextContent);
        Assert.Equal("ennui", page.Find(".wotd-term").TextContent);
        Assert.Contains("You’re not reading anything right now.", page.Find(".reading").TextContent);
    }

    [Fact]
    public async Task Reading_card_shows_the_book_and_how_long_it_has_been()
    {
        var book = await AddBookAsync("Dune", "Frank Herbert");
        await AddSessionAsync(book.Id, new ReadingSessionInput { Status = ReadingStatus.Reading, StartedOn = Today.AddDays(-12) });

        var page = Render<HomePage>();

        page.WaitForAssertion(() => page.Find(".reading-card"));
        Assert.Equal("Dune", page.Find(".reading-card .book-title").TextContent);
        Assert.Equal("Frank Herbert", page.Find(".reading-card .authors").TextContent);
        Assert.EndsWith("· day 13", page.Find(".reading-card .since").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Finishing_from_the_reading_card_moves_the_book_to_recently_finished()
    {
        var book = await AddBookAsync("Dune");
        await AddSessionAsync(book.Id, new ReadingSessionInput { Status = ReadingStatus.Reading, StartedOn = Today.AddDays(-3) });
        var page = Render<HomePage>();
        page.WaitForAssertion(() => page.Find(".reading-card"));

        page.FindAll(".reading-card button").Single(b => b.TextContent == "Finished").Click();
        page.FindAll(".panel .rating-input .star")[4].Click();
        page.Find(".panel form").Submit();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".reading-card")));
        Assert.Equal("Dune", page.Find(".finished-card .title").TextContent);
        Assert.Contains("Rated 5", page.Find(".finished-card .stars").GetAttribute("aria-label"), StringComparison.Ordinal);
        var session = Assert.Single((await GetBookAsync(book.Id)).ReadingSessions);
        Assert.Equal(ReadingStatus.Finished, session.Status);
        Assert.Equal(Today, session.FinishedOn);
        Assert.Equal("1", page.FindAll(".stats dd")[0].TextContent);
    }

    [Fact]
    public async Task Add_word_from_the_reading_card_links_it_to_the_book_and_stays_open()
    {
        var book = await AddBookAsync("Dune");
        await AddSessionAsync(book.Id, new ReadingSessionInput { Status = ReadingStatus.Reading, StartedOn = Today });
        var page = Render<HomePage>();
        page.WaitForAssertion(() => page.Find(".reading-card"));

        page.FindAll(".reading-card button").Single(b => b.TextContent == "Add word").Click();
        page.Find(".panel input").Change("kanly");
        page.Find(".panel input[inputmode=numeric]").Change("42");
        page.Find(".panel form").Submit();

        page.WaitForAssertion(() => Assert.Contains("Added kanly", page.Find(".panel .word-added").TextContent));
        Assert.Equal("42", page.Find(".panel input[inputmode=numeric]").GetAttribute("value"));
        Assert.Equal("kanly", page.Find(".recent-term").TextContent);
        Assert.Equal(1, (await GetBookAsync(book.Id)).VocabularyWordCount);
    }

    [Fact]
    public async Task Another_shows_a_different_defined_word()
    {
        Dictionary.Define("ennui", "Listlessness.");
        Dictionary.Define("sonder", "Awareness of others' lives.");
        await AddWordAsync("ennui");
        await AddWordAsync("sonder");
        await AddWordAsync("undefined");
        var page = Render<HomePage>();
        page.WaitForAssertion(() => page.Find(".word-of-the-day"));
        Assert.Equal("Word of the day", page.Find("#wotd-heading").TextContent);
        var first = page.Find(".wotd-term").TextContent;

        page.Find(".word-of-the-day button").Click();

        page.WaitForAssertion(() => Assert.NotEqual(first, page.Find(".wotd-term").TextContent));
        Assert.True(page.Find(".wotd-term").TextContent is "ennui" or "sonder");
        Assert.Equal("Another word", page.Find("#wotd-heading").TextContent);
    }

    [Fact]
    public async Task Word_of_the_day_is_hidden_without_defined_words()
    {
        await AddWordAsync("undefined");

        var page = Render<HomePage>();

        page.WaitForAssertion(() => page.Find(".stats"));
        Assert.Empty(page.FindAll(".word-of-the-day"));
        Assert.Equal("undefined", page.Find(".recent-term").TextContent);
    }

    private async Task AddSessionAsync(Guid bookId, ReadingSessionInput input) =>
        Success(await Library.AddReadingSessionAsync(bookId, input, Ct));
}
