using Bookside.Core.Books.Services;
using Bookside.UI.Pages;

namespace Bookside.UI.Tests.Components;

public sealed class BookEditPageTests : ComponentTestBase
{
    [Fact]
    public async Task Adding_a_book_saves_it_and_opens_its_page()
    {
        var page = Render<BookEditPage>();

        page.Find("#title").Change("  The Dispossessed ");
        page.Find("#author-0").Change("Ursula K. Le Guin");
        page.Find("button.btn-sm").Click(); // add another author
        page.Find("#author-1").Change("Second Author");
        page.Find("#isbn10").Change("0-06-051275-X");
        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.Matches("/books/[0-9a-f-]{36}$", Navigation.Uri));
        var id = Guid.Parse(Navigation.Uri[^36..]);
        var book = await GetBookAsync(id);
        Assert.Equal("The Dispossessed", book.Title);
        Assert.Equal(["Ursula K. Le Guin", "Second Author"], book.Authors);
        Assert.Equal("9780060512750", book.Isbn13);
    }

    [Fact]
    public void Service_validation_errors_show_next_to_their_fields()
    {
        var page = Render<BookEditPage>();

        page.Find("#isbn13").Change("123");
        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".validation-message").Count));
        var messages = page.FindAll(".validation-message").Select(m => m.TextContent).ToList();
        Assert.Contains("Required.", messages);
        Assert.Contains("Not a valid ISBN-13.", messages);
        Assert.Equal("http://localhost/", Navigation.Uri);
    }

    [Fact]
    public void A_field_error_clears_when_that_field_is_edited()
    {
        var page = Render<BookEditPage>();
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".validation-message")));

        page.Find("#title").Change("Fixed");

        Assert.Empty(page.FindAll(".validation-message"));
    }

    [Fact]
    public async Task Editing_prefills_and_keeps_metadata_source()
    {
        var book = Success(await Library.AddBookAsync(
            new BookInput { Title = "Old", Authors = ["A", "B"], MetadataSource = "openlibrary", MetadataSourceId = "OL1M" },
            Ct));

        var page = Render<BookEditPage>(p => p.Add(x => x.Id, book.Id));
        page.WaitForAssertion(() => Assert.Equal("Old", page.Find("#title").GetAttribute("value")));
        Assert.Equal("B", page.Find("#author-1").GetAttribute("value"));

        page.Find("#title").Change("New");
        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.EndsWith($"/books/{book.Id}", Navigation.Uri, StringComparison.Ordinal));
        var saved = await GetBookAsync(book.Id);
        Assert.Equal("New", saved.Title);
        Assert.Equal(["A", "B"], saved.Authors);
        Assert.Equal("openlibrary", saved.MetadataSource);
        Assert.Equal("OL1M", saved.MetadataSourceId);
    }

    [Fact]
    public void Editing_a_missing_book_says_so()
    {
        var page = Render<BookEditPage>(p => p.Add(x => x.Id, Guid.NewGuid()));

        page.WaitForAssertion(() => Assert.Contains("This book no longer exists.", page.Markup));
    }
}
