using Bookside.Core.Books.Services;
using Bookside.Core.Vocabulary.Services;

namespace Bookside.Data.Tests.Services;

public sealed class SearchServiceTests : ServiceTestBase
{
    private const int MaxBooks = 6; // SearchService.MaxBooks

    [Fact]
    public async Task Books_match_title_author_and_series_with_title_prefixes_first()
    {
        await AddBookAsync("Small Gods", "Terry Pratchett");
        await AddBookAsync("Godshome", "Someone Else");
        await AddBookAsync("Dune", "Frank Herbert");
        Success(await Library.AddBookAsync(
            new BookInput { Title = "Mort", Series = "Gods of Discworld" },
            Ct));
        await AddBookAsync("Eric", "A. Godwin");

        var results = await Search.SearchAsync(" god ", Ct);

        Assert.Equal("god", results.Query);
        Assert.Equal(
            ["Godshome", "Eric", "Mort", "Small Gods"],
            results.Books.Items.Select(b => b.Title));
        Assert.False(results.Books.HasMore);
    }

    [Fact]
    public async Task Words_match_their_term_case_insensitively()
    {
        Dictionary.Define("melancholy", "Sadness.");
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "Melancholy" }, Ct));
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "cholera" }, Ct));
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "wistful" }, Ct));

        var results = await Search.SearchAsync("CHOL", Ct);

        Assert.Equal(["cholera", "Melancholy"], results.Words.Items.Select(w => w.Term));
        Assert.Equal("Sadness.", results.Words.Items[1].ShortDefinition);
    }

    [Fact]
    public async Task Tags_match_by_name_with_book_counts()
    {
        var mort = await AddBookAsync("Mort");
        var dune = await AddBookAsync("Dune");
        Success(await Tags.SetBookTagsAsync(mort.Id, ["Own", "Unowned"], Ct));
        Success(await Tags.SetBookTagsAsync(dune.Id, ["Own"], Ct));

        var results = await Search.SearchAsync("own", Ct);

        Assert.Equal(
            [("Own", 2), ("Unowned", 1)],
            results.Tags.Items.Select(t => (t.Name, t.BookCount)));
    }

    [Fact]
    public async Task Comments_match_their_text_newest_first_with_a_snippet()
    {
        var mort = await AddBookAsync("Mort");
        var dune = await AddBookAsync("Dune");
        Success(await Library.AddCommentAsync(
            mort.Id,
            new BookCommentInput { Text = "@[Death] takes an apprentice." },
            Ct));
        Success(await Library.AddCommentAsync(
            dune.Id,
            new BookCommentInput { Text = "The spice must flow.\nAn apprentice of sorts." },
            Ct));
        Success(await Library.AddCommentAsync(
            dune.Id,
            new BookCommentInput { Text = "Nothing to see." },
            Ct));

        var results = await Search.SearchAsync("apprentice", Ct);

        Assert.Equal(
            [
                ("Dune", "The spice must flow. An apprentice of sorts."),
                ("Mort", "@Death takes an apprentice."),
            ],
            results.Comments.Items.Select(c => (c.BookTitle, c.Snippet)));
        Assert.Equal(dune.Id, results.Comments.Items[0].BookId);
    }

    [Fact]
    public async Task Books_and_comments_match_any_apostrophe()
    {
        var ender = await AddBookAsync("Ender’s Game");
        Success(await Library.AddCommentAsync(
            ender.Id,
            new BookCommentInput { Text = "Ender’s last game." },
            Ct));

        var results = await Search.SearchAsync("ender's", Ct);

        Assert.Equal(["Ender’s Game"], results.Books.Items.Select(b => b.Title));
        Assert.Equal(["Ender’s last game."], results.Comments.Items.Select(c => c.Snippet));
    }

    [Fact]
    public async Task Comments_match_tags_typed_the_way_they_display()
    {
        var mort = await AddBookAsync("Mort");
        Success(await Library.AddCommentAsync(
            mort.Id,
            new BookCommentInput { Text = "Lunch with @[Fred Test] and #[slow burn]." },
            Ct));

        var person = await Search.SearchAsync("@Fred", Ct);
        var general = await Search.SearchAsync("#slow b", Ct);

        Assert.Equal(
            "Lunch with @Fred Test and #slow burn.",
            Assert.Single(person.Comments.Items).Snippet);
        Assert.Single(general.Comments.Items);
    }

    [Fact]
    public async Task Comments_matching_only_a_page_tag_are_not_found()
    {
        var mort = await AddBookAsync("Mort");
        Success(await Library.AddCommentAsync(
            mort.Id,
            new BookCommentInput { Text = "Great scene on p.142." },
            Ct));
        Success(await Library.AddCommentAsync(
            mort.Id,
            new BookCommentInput { Text = "Counted 142 hourglasses." },
            Ct));

        var results = await Search.SearchAsync("142", Ct);

        Assert.Equal(
            ["Counted 142 hourglasses."],
            results.Comments.Items.Select(c => c.Snippet));
    }

    [Fact]
    public async Task Sections_are_capped_and_report_more()
    {
        for (var i = 0; i <= MaxBooks; i++)
        {
            await AddBookAsync($"Book {i}");
        }

        var results = await Search.SearchAsync("book", Ct);

        Assert.Equal(MaxBooks, results.Books.Items.Count);
        Assert.True(results.Books.HasMore);
        Assert.Equal("Book 0", results.Books.Items[0].Title);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(" Mo ")]
    public async Task A_query_under_the_minimum_length_returns_empty_sections(string query)
    {
        await AddBookAsync("Mort");

        var results = await Search.SearchAsync(query, Ct);

        Assert.True(results.IsEmpty);
        Assert.Equal(query.Trim(), results.Query);
    }

    [Fact]
    public async Task Wildcards_in_the_query_are_matched_literally()
    {
        await AddBookAsync("100% Wool");
        await AddBookAsync("1000 Years");
        await AddBookAsync("snake_case");
        await AddBookAsync("snakes");

        var percent = await Search.SearchAsync("100%", Ct);
        var underscore = await Search.SearchAsync("e_c", Ct);

        Assert.Equal(["100% Wool"], percent.Books.Items.Select(b => b.Title));
        Assert.Equal(["snake_case"], underscore.Books.Items.Select(b => b.Title));
    }
}
