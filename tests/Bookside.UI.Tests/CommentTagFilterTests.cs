using Bookside.Core.Books;
using Bookside.Core.Books.Services;

namespace Bookside.UI.Tests;

public sealed class CommentTagFilterTests
{
    [Fact]
    public void Chips_list_each_tag_once_people_then_general_then_pages_in_page_order()
    {
        var chips = CommentTagFilter.Chips(
        [
            Comment("p.112 @Rochester #gothic p.42"),
            Comment("@[Jane Eyre] @rochester #Gothic p.9"),
        ]);

        Assert.Equal(
            ["@Jane Eyre", "@Rochester", "#gothic", "p. 9", "p. 42", "p. 112"],
            chips.Select(c => c.Display));
        Assert.Equal("@[Jane Eyre]", chips[0].Key);
    }

    [Fact]
    public void Parse_reads_one_tag_and_has_matches_any_spelling()
    {
        var tag = CommentTagFilter.Parse("@[jane eyre]");

        Assert.NotNull(tag);
        Assert.True(CommentTagFilter.Has(Comment("Met @[Jane  Eyre]."), tag));
        Assert.False(CommentTagFilter.Has(Comment("#[jane eyre]"), tag)); // other type
        Assert.Null(CommentTagFilter.Parse("plain"));
        Assert.Null(CommentTagFilter.Parse("@a @b"));
        Assert.Null(CommentTagFilter.Parse(null));
    }

    private static BookCommentDto Comment(string text) =>
        new() { Id = Guid.NewGuid(), BookId = Guid.Empty, Text = text };
}
