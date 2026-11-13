namespace Bookside.UI.Tests;

public sealed class SearchHighlightTests
{
    [Fact]
    public void Marks_every_match_ignoring_case_and_keeps_all_text()
    {
        var segments = SearchHighlight.Split("Gods of the Small Gods", " GOD ");

        Assert.Equal(
            [
                ("God", true),
                ("s of the Small ", false),
                ("God", true),
                ("s", false),
            ],
            segments.Select(s => (s.Text, s.IsMatch)));
    }

    [Fact]
    public void Any_apostrophe_matches_any_other_and_keeps_the_text()
    {
        var segments = SearchHighlight.Split("Ender’s Game", "ender's");

        Assert.Equal(
            [("Ender’s", true), (" Game", false)],
            segments.Select(s => (s.Text, s.IsMatch)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("xyz")]
    public void Blank_or_missing_query_leaves_the_text_unmarked(string? query) =>
        Assert.Equal(
            [("Mort", false)],
            SearchHighlight.Split("Mort", query).Select(s => (s.Text, s.IsMatch)));

    [Fact]
    public void Empty_text_has_no_segments() =>
        Assert.Empty(SearchHighlight.Split("", "x"));
}
