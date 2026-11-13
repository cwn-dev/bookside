using Bookside.Core.Books.Services;
using Bookside.UI.Pages;

namespace Bookside.UI.Tests.Components;

public sealed class SeriesPageTests : ComponentTestBase
{
    [Fact]
    public void No_series_explains_where_to_set_one()
    {
        var page = Render<SeriesPage>();

        page.WaitForAssertion(() => Assert.Contains("No series yet.", page.Find(".empty").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Lists_each_series_with_its_books_in_order()
    {
        var mort = Success(await Library.AddBookAsync(new BookInput { Title = "Mort", Series = "Discworld", SeriesNumber = 4 }, Ct));
        Success(await Library.AddBookAsync(new BookInput { Title = "Equal Rites", Series = "Discworld", SeriesNumber = 3 }, Ct));

        var page = Render<SeriesPage>();

        page.WaitForAssertion(() => Assert.Equal("Discworld", page.Find(".series h2").TextContent));
        Assert.Equal("books?q=Discworld", page.Find(".series h2 a").GetAttribute("href"));
        Assert.Equal(["Equal Rites", "Mort"], page.FindAll(".shelf a").Select(a => a.GetAttribute("title")));
        Assert.Equal($"books/{mort.Id}", page.FindAll(".shelf a")[1].GetAttribute("href"));
        Assert.Equal(["#3", "#4"], page.FindAll(".shelf .number").Select(n => n.TextContent));
    }
}
