using Bookside.UI.Pages;

namespace Bookside.UI.Tests.Components;

public sealed class TagsPageTests : ComponentTestBase
{
    [Fact]
    public void No_tags_explains_where_to_add_them()
    {
        var page = Render<TagsPage>();

        page.WaitForAssertion(() => Assert.Contains("No tags yet.", page.Find(".empty").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Lists_tags_with_book_counts_linking_to_the_library()
    {
        var mort = await AddBookAsync("Mort");
        var dune = await AddBookAsync("Dune");
        Success(await Tags.SetBookTagsAsync(mort.Id, ["Own", "Have e-book"], Ct));
        Success(await Tags.SetBookTagsAsync(dune.Id, ["Own"], Ct));

        var page = Render<TagsPage>();

        page.WaitForAssertion(() => Assert.Equal(["Have e-book", "Own"], page.FindAll(".tag-row .name").Select(n => n.TextContent)));
        var counts = page.FindAll(".tag-row a.count");
        Assert.Equal(["1 book", "2 books"], counts.Select(c => c.TextContent));
        Assert.Equal("books?tag=Have%20e-book", counts[0].GetAttribute("href"));
    }

    [Fact]
    public async Task Rename_saves_or_shows_a_duplicate_name_conflict()
    {
        var mort = await AddBookAsync("Mort");
        Success(await Tags.SetBookTagsAsync(mort.Id, ["own", "Signed"], Ct));
        var page = Render<TagsPage>();
        page.WaitForAssertion(() => page.Find(".tag-row"));

        page.FindAll(".tag-row button").First(b => b.TextContent == "Rename").Click();
        page.Find(".rename input").Change("Signed");
        page.Find(".rename").Submit();
        page.WaitForAssertion(() => Assert.Contains("already a tag called", page.Find(".rename .alert").TextContent, StringComparison.Ordinal));

        page.Find(".rename input").Change("Own");
        page.Find(".rename").Submit();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".rename")));
        Assert.Equal(["Own", "Signed"], page.FindAll(".tag-row .name").Select(n => n.TextContent));
        Assert.Equal(["Own", "Signed"], (await GetBookAsync(mort.Id)).Tags);
    }

    [Fact]
    public async Task Delete_asks_first_then_removes_the_tag_from_its_books()
    {
        var mort = await AddBookAsync("Mort");
        Success(await Tags.SetBookTagsAsync(mort.Id, ["Own", "Signed"], Ct));
        var page = Render<TagsPage>();
        page.WaitForAssertion(() => page.Find(".tag-row"));

        page.FindAll(".tag-row")[0].QuerySelectorAll("button").Single(b => b.TextContent == "Delete").Click();
        Assert.Contains("Remove it from 1 book and delete it?", page.Find(".tag-row").TextContent, StringComparison.Ordinal);
        page.FindAll(".tag-row")[0].QuerySelectorAll("button").Single(b => b.TextContent == "Delete").Click();

        page.WaitForAssertion(() => Assert.Equal(["Signed"], page.FindAll(".tag-row .name").Select(n => n.TextContent)));
        Assert.Equal(["Signed"], (await GetBookAsync(mort.Id)).Tags);
    }
}
