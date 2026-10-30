using Bookside.Core.Vocabulary.Services;
using Bookside.UI.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.UI.Tests.Components;

public sealed class ReviewPageTests : ComponentTestBase
{
    private IReviewService Reviews => Services.GetRequiredService<IReviewService>();

    [Fact]
    public void Without_defined_words_explains_where_cards_come_from()
    {
        var page = Render<ReviewPage>();

        page.WaitForAssertion(() => Assert.Contains("No flash cards yet", page.Find(".summary").TextContent));
    }

    [Fact]
    public async Task Shows_the_word_then_the_answer_with_where_it_was_found()
    {
        Dictionary.Define("ennui", "Listlessness.", example: "A wave of ennui.");
        var book = await AddBookAsync("Mort");
        await AddWordAsync("Ennui", book.Id, context: "Ennui set in.", page: 42);

        var page = Render<ReviewPage>();

        page.WaitForAssertion(() => Assert.Equal("Ennui", page.Find(".term").TextContent));
        Assert.Equal("/test/", page.Find(".phonetic").TextContent);
        Assert.Equal("New", page.Find(".front .badge").TextContent);
        Assert.Contains("1 of 1", page.Find(".progress").TextContent);
        Assert.Empty(page.FindAll(".answer"));

        page.Find("button.reveal").Click();

        Assert.Contains("Listlessness.", page.Find(".meaning li").TextContent);
        Assert.Contains("Mort", page.Find(".found-in").TextContent);
        Assert.Contains("page 42", page.Find(".found-in").TextContent);
        Assert.Equal("Ennui set in.", page.Find(".context").TextContent);
        Assert.Equal("tomorrow", page.Find(".forgot .hint").TextContent);
        Assert.Equal("tomorrow", page.Find(".knew .hint").TextContent);
    }

    [Fact]
    public async Task Knew_it_records_the_review_and_finishes()
    {
        Dictionary.Define("ennui", "Listlessness.");
        await AddWordAsync("ennui");
        var page = Render<ReviewPage>();
        page.WaitForAssertion(() => page.Find("button.reveal"));

        page.Find("button.reveal").Click();
        page.Find("button.knew").Click();

        page.WaitForAssertion(() => Assert.Contains("You reviewed 1 word.", page.Find(".summary").TextContent));
        Assert.Contains("Next review tomorrow.", page.Find(".summary").TextContent);
        Assert.Empty(page.FindAll("button.practise.forgotten"));
        Assert.NotNull(page.Find("button.practise.random"));
        Assert.Equal(0, (await Reviews.GetStatusAsync(Ct)).ToReviewCount);
    }

    [Fact]
    public async Task A_forgotten_word_is_not_repeated_in_the_scored_pass()
    {
        Dictionary.Define("ennui", "Listlessness.");
        Dictionary.Define("petrichor", "The smell of rain.");
        await AddWordAsync("ennui");
        await AddWordAsync("petrichor");
        var page = Render<ReviewPage>();
        page.WaitForAssertion(() => page.Find("button.reveal"));

        page.Find("button.reveal").Click();
        page.Find("button.forgot").Click();
        page.WaitForAssertion(() => Assert.Equal("petrichor", page.Find(".term").TextContent));
        Assert.Contains("2 of 2", page.Find(".progress").TextContent);
        page.Find("button.reveal").Click();
        page.Find("button.knew").Click();

        page.WaitForAssertion(() => Assert.Contains(
            "You reviewed 2 words; 1 word will come back tomorrow.",
            page.Find(".summary").TextContent));
        Assert.Contains("Forgotten today (1)", page.Find("button.practise.forgotten").TextContent);
    }

    [Fact]
    public async Task Practice_round_repeats_forgotten_words_without_recording()
    {
        Dictionary.Define("ennui", "Listlessness.");
        await AddWordAsync("ennui");
        var page = Render<ReviewPage>();
        page.WaitForAssertion(() => page.Find("button.reveal"));
        page.Find("button.reveal").Click();
        page.Find("button.forgot").Click();
        page.WaitForAssertion(() => page.Find("button.practise.forgotten"));
        var before = await Reviews.GetStatusAsync(Ct);

        page.Find("button.practise.forgotten").Click();

        page.WaitForAssertion(() => Assert.Equal("ennui", page.Find(".term").TextContent));
        Assert.Contains("Practice", page.Find(".progress").TextContent);
        Assert.Contains("don't change your schedule", page.Find(".practice-note").TextContent);
        Assert.Empty(page.FindAll(".front .badge"));
        page.Find("button.reveal").Click();
        Assert.Empty(page.FindAll(".grade .hint"));
        page.Find("button.forgot").Click();
        page.WaitForAssertion(() => Assert.Contains(
            "2 of 2 · 1 added back",
            page.Find(".progress").TextContent));
        page.Find("button.reveal").Click();
        page.Find("button.knew").Click();

        page.WaitForAssertion(() => Assert.Contains("Practice done", page.Find(".summary").TextContent));
        Assert.Contains("Practise again", page.Find(".practice").TextContent);
        Assert.Equal(before, await Reviews.GetStatusAsync(Ct));
    }

    [Fact]
    public async Task With_nothing_due_practice_can_still_be_chosen()
    {
        Dictionary.Define("ennui", "Listlessness.");
        var word = await AddWordAsync("ennui");
        Assert.True((await Reviews.RecordReviewAsync(word.Id, knew: true, Ct)).IsSuccess);
        var page = Render<ReviewPage>();
        page.WaitForAssertion(() => Assert.Contains("Nothing to review", page.Find(".summary").TextContent));

        page.Find("button.practise.hardest").Click();
        page.WaitForAssertion(() => Assert.Contains(
            "None of your words has been forgotten yet.",
            page.Find(".practice").TextContent));

        page.Find("button.practise.random").Click();
        page.WaitForAssertion(() => Assert.Equal("ennui", page.Find(".term").TextContent));
        Assert.Contains("Practice", page.Find(".progress").TextContent);
    }

    [Fact]
    public async Task Practice_rounds_are_history_entries_so_back_returns_to_the_review()
    {
        Dictionary.Define("ennui", "Listlessness.");
        var word = await AddWordAsync("ennui");
        Assert.True((await Reviews.RecordReviewAsync(word.Id, knew: false, Ct)).IsSuccess);
        var page = Render<ReviewPage>();
        page.WaitForAssertion(() => page.Find("button.practise.forgotten"));

        page.Find("button.practise.forgotten").Click();

        page.WaitForAssertion(() => Assert.Equal("ennui", page.Find(".term").TextContent));
        Assert.EndsWith("/words/review?practice=forgotten", Navigation.Uri, StringComparison.Ordinal);

        Navigation.NavigateTo("words/review");

        page.WaitForAssertion(() => Assert.Contains("Nothing to review", page.Find(".summary").TextContent));
        Assert.Empty(page.FindAll(".progress"));
    }

    [Fact]
    public async Task Number_keys_grade_once_the_answer_is_shown()
    {
        Dictionary.Define("ennui", "Listlessness.");
        await AddWordAsync("ennui");
        var page = Render<ReviewPage>();
        page.WaitForAssertion(() => page.Find("button.reveal"));

        page.Find(".flashcard").KeyDown("2");
        Assert.NotNull(page.Find("button.reveal"));

        page.Find("button.reveal").Click();
        page.Find(".flashcard").KeyDown("2");

        page.WaitForAssertion(() => Assert.Contains("You reviewed 1 word.", page.Find(".summary").TextContent));
    }

    [Fact]
    public async Task Home_page_shows_the_review_card_only_with_words_to_review()
    {
        await AddWordAsync("brillig");
        var page = Render<HomePage>();
        page.WaitForAssertion(() => page.Find(".stats"));
        Assert.Empty(page.FindAll("section.review"));

        Dictionary.Define("ennui", "Listlessness.");
        await AddWordAsync("ennui");
        page = Render<HomePage>();

        page.WaitForAssertion(() => Assert.Contains("1 word to review", page.Find("section.review").TextContent));
        Assert.Equal("words/review", page.Find("section.review a").GetAttribute("href"));
    }

    [Fact]
    public async Task Home_page_says_all_done_once_the_review_is_finished()
    {
        Dictionary.Define("ennui", "Listlessness.");
        var word = await AddWordAsync("ennui");
        Assert.True((await Reviews.RecordReviewAsync(word.Id, knew: true, Ct)).IsSuccess);

        var page = Render<HomePage>();

        page.WaitForAssertion(() => Assert.Contains(
            "All done · next review tomorrow",
            page.Find("section.review").TextContent));
        Assert.Equal("Practise", page.Find("section.review a").TextContent);
    }
}
