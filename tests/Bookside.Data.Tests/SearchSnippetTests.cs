using Bookside.Core.Search;

namespace Bookside.Data.Tests;

public sealed class SearchSnippetTests
{
    private const int Max = 40;

    [Fact]
    public void Short_text_is_returned_whole_with_tags_in_display_form()
    {
        var snippet = SearchSnippet.Around("@[Jane Eyre] on p.42,\n  #[slow burn]", "jane");

        Assert.Equal("@Jane Eyre on p. 42, #slow burn", snippet);
    }

    [Fact]
    public void A_match_near_the_start_cuts_only_the_end_at_a_space()
    {
        var text = "Needle in the first words, then a long tail of unrelated text follows.";

        var snippet = SearchSnippet.Around(text, "needle", Max);

        Assert.Equal("Needle in the first words, then a long…", snippet);
    }

    [Fact]
    public void A_match_in_the_middle_keeps_some_lead_up_and_cuts_both_ends()
    {
        var text = "Lots of words come first here and then the needle appears, "
            + "followed by many more words at the end.";

        var snippet = SearchSnippet.Around(text, "NEEDLE", Max);

        Assert.StartsWith("…", snippet, StringComparison.Ordinal);
        Assert.EndsWith("…", snippet, StringComparison.Ordinal);
        Assert.Contains("needle", snippet, StringComparison.Ordinal);
        Assert.True(snippet!.Length <= Max + 2);
        Assert.DoesNotContain("  ", snippet, StringComparison.Ordinal);
    }

    [Fact]
    public void A_match_at_the_end_cuts_only_the_start()
    {
        var text = "A long opening with plenty of words before the final needle";

        var snippet = SearchSnippet.Around(text, "needle", Max);

        Assert.StartsWith("…", snippet, StringComparison.Ordinal);
        Assert.EndsWith("final needle", snippet, StringComparison.Ordinal);
    }

    [Fact]
    public void Tags_are_never_split_by_a_cut()
    {
        var text = "Some opening words that go on for a while @[Ann Smith] says the needle";

        var snippet = SearchSnippet.Around(text, "needle", Max);

        Assert.DoesNotContain("[", snippet, StringComparison.Ordinal);
        Assert.DoesNotContain("]", snippet, StringComparison.Ordinal);
    }

    [Fact]
    public void No_match_gives_null_and_a_blank_query_shows_the_start()
    {
        var text = "The quick brown fox jumps over the lazy dog, again and again and again.";

        Assert.Null(SearchSnippet.Around(text, "[fox", Max));
        Assert.StartsWith(
            "The quick brown fox",
            SearchSnippet.Around(text, " ", Max),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Matches_inside_unsearchable_page_tags_do_not_count()
    {
        Assert.Null(SearchSnippet.Around("Loved p.42 so much.", "42"));
        Assert.Null(SearchSnippet.Around("Loved p. 42 so much.", "p. 4"));
        Assert.Equal(
            "On p. 42 the 42nd step.",
            SearchSnippet.Around("On  p.42\nthe 42nd step.", "42"));
    }
}
