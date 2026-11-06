using Bookside.Core.Books;

namespace Bookside.Data.Tests;

public sealed class CommentTagSyntaxTests
{
    [Fact]
    public void Only_page_tags_are_unsearchable()
    {
        Assert.True(CommentTagSyntax.IsSearchable(CommentTagType.Person));
        Assert.True(CommentTagSyntax.IsSearchable(CommentTagType.General));
        Assert.False(CommentTagSyntax.IsSearchable(CommentTagType.Page));
    }

    [Theory]
    [InlineData("@Fred", "@[Fred")]
    [InlineData("@Fred Test and #grim", "@[Fred Test and #[grim")]
    [InlineData("me@x.com", "me@x.com")]
    [InlineData("@ #1 @[Ann", "@ #1 @[Ann")]
    [InlineData("p. 42", "p. 42")]
    public void To_stored_search_brackets_tags_typed_as_shown(string query, string expected) =>
        Assert.Equal(expected, CommentTagSyntax.ToStoredSearch(query));

    [Fact]
    public void Parse_finds_bracketed_and_bare_tags_and_keeps_all_text()
    {
        const string text = "@[Jane Eyre] meets @Rochester. #gothic! (p. 42)";

        var segments = CommentTagSyntax.Parse(text);

        Assert.Equal(text, string.Concat(segments.Select(s => s.Text)));
        Assert.Equal(
            [
                (CommentTagType.Person, "Jane Eyre"),
                (CommentTagType.Person, "Rochester"),
                (CommentTagType.General, "gothic"),
                (CommentTagType.Page, "42"),
            ],
            segments
                .Where(s => s.TagType is not null)
                .Select(s => (s.TagType!.Value, s.TagName!)));
        Assert.Equal(" meets ", segments[1].Text);
    }

    [Theory]
    [InlineData("mail me@example.com")] // trigger after a letter
    [InlineData("the #1 bestseller")] // bare word must start with a letter
    [InlineData("@[Jane\nEyre]")] // no line breaks in brackets
    [InlineData("@[Jane Eyre")] // unclosed
    [InlineData("@[   ]")] // blank
    [InlineData("@ alone #")]
    [InlineData("chap.5")] // page trigger after a letter
    [InlineData("p.0 p.x p.12a p.  4")] // zero, no digits, a word, two spaces
    [InlineData("p.1234567")] // too many digits
    public void Parse_ignores_things_that_are_not_tags(string text) =>
        Assert.DoesNotContain(CommentTagSyntax.Parse(text), s => s.TagType is not null);

    [Fact]
    public void Parse_trims_bracketed_names_and_limits_their_length()
    {
        var trimmed = Assert.Single(CommentTagSyntax.Parse("#[ slow burn ]"));
        Assert.Equal("slow burn", trimmed.TagName);

        var tooLong = $"@[{new string('x', CommentTag.NameMaxLength + 1)}]";
        Assert.Null(Assert.Single(CommentTagSyntax.Parse(tooLong)).TagType);
    }

    [Theory]
    [InlineData("p.42", "42")]
    [InlineData("p. 42", "42")]
    [InlineData("p.007", "7")]
    public void Parse_reads_pages(string text, string page) =>
        Assert.Equal(page, Assert.Single(CommentTagSyntax.Parse(text)).TagName);

    [Fact]
    public void Format_brackets_names_and_display_spaces_pages()
    {
        Assert.Equal("@[Jane]", CommentTagSyntax.Format(CommentTagType.Person, "Jane"));
        Assert.Equal("p.42", CommentTagSyntax.Format(CommentTagType.Page, "42"));
        Assert.Equal("p. 42", CommentTagSyntax.Display(CommentTagType.Page, "42"));
        Assert.Equal("#theme", CommentTagSyntax.Display(CommentTagType.General, "theme"));
    }

    [Theory]
    [InlineData("Met @Ja", 7, 4, CommentTagType.Person, "Ja")]
    [InlineData("Met @[Mary Ann Ev", 17, 4, CommentTagType.Person, "Mary Ann Ev")]
    [InlineData("Met @Mary Ann", 13, 4, CommentTagType.Person, "Mary Ann")]
    [InlineData("#", 1, 0, CommentTagType.General, "")]
    [InlineData("See p.4", 7, 4, CommentTagType.Page, "4")]
    [InlineData("See p. 12", 9, 4, CommentTagType.Page, "12")]
    [InlineData("p.", 2, 0, CommentTagType.Page, "")]
    [InlineData("@Ja and more", 3, 0, CommentTagType.Person, "Ja")] // caret mid-text
    public void FindOpen_returns_the_tag_before_the_caret(
        string text,
        int caret,
        int start,
        CommentTagType type,
        string query) =>
            Assert.Equal(new OpenCommentTag(start, type, query), CommentTagSyntax.FindOpen(text, caret));

    [Theory]
    [InlineData("@[Jane] and", 11)] // closed tag
    [InlineData("@Jane\nnext", 10)] // new line
    [InlineData("me@x", 4)] // not a boundary
    [InlineData("#1", 2)]
    [InlineData("p.4x", 4)] // pages are digits only
    [InlineData("chap.4", 6)]
    [InlineData("p.42 next", 9)]
    [InlineData("@Jane went to the shops", 23)] // too many words for a bare tag
    public void FindOpen_returns_null_when_no_tag_is_being_typed(string text, int caret) =>
        Assert.Null(CommentTagSyntax.FindOpen(text, caret));
}
