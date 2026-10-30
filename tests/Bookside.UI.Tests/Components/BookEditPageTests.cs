using Bookside.Core.Books.Lookup;
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
    public async Task Picking_a_lookup_result_fills_only_empty_fields_and_adds_the_cover_on_save()
    {
        BookLookup.Results =
        [
            new BookMetadata
            {
                Source = "openlibrary",
                SourceId = "/books/OL1M",
                Title = "Mort",
                Authors = ["Terry Pratchett"],
                Publisher = "Corgi",
                PageCount = 288,
                CoverImageUrl = new Uri("https://covers.openlibrary.org/b/id/1-L.jpg"),
            },
        ];
        var page = Render<BookEditPage>();
        page.Find("#title").Change("My title");

        page.Find("#lookup-query").Input("mort");
        page.Find(".lookup-row button").Click();
        var result = page.WaitForElement(".lookup-results button");
        Assert.Equal("Open Library", result.QuerySelector(".source-label")?.TextContent);
        result.Click();

        Assert.Equal("My title", page.Find("#title").GetAttribute("value"));
        Assert.Equal("Terry Pratchett", page.Find("#author-0").GetAttribute("value"));
        Assert.Equal("Corgi", page.Find("#publisher").GetAttribute("value"));
        Assert.Contains(
            "Open Library",
            page.Find(".book-lookup .alert-success").TextContent,
            StringComparison.Ordinal);

        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.Matches("/books/[0-9a-f-]{36}$", Navigation.Uri));
        var book = await GetBookAsync(Guid.Parse(Navigation.Uri[^36..]));
        Assert.Equal(("My title", 288), (book.Title, book.PageCount));
        Assert.Equal(("openlibrary", "/books/OL1M"), (book.MetadataSource, book.MetadataSourceId));
        Assert.NotNull(book.CoverSha256);
    }

    [Fact]
    public void Picking_a_result_fills_the_description_from_its_details()
    {
        BookLookup.Results = [LookupResult("Mort") with { Description = "One. Two." }];
        BookLookup.Description = "One.\n\nTwo.";
        var page = Render<BookEditPage>();

        PickFirstResult(page, "mort");

        Assert.Equal("One.\n\nTwo.", page.Find("#description").GetAttribute("value"));
    }

    [Fact]
    public void Picking_a_result_whose_details_fail_fills_from_the_search_result()
    {
        BookLookup.Results = [LookupResult("Mort") with { Description = "One. Two." }];
        BookLookup.DetailsUnavailable = true;
        var page = Render<BookEditPage>();

        PickFirstResult(page, "mort");

        Assert.Equal("One. Two.", page.Find("#description").GetAttribute("value"));
        Assert.Equal("Mort", page.Find("#title").GetAttribute("value"));
    }

    [Fact]
    public void Saving_shows_that_the_cover_is_downloading()
    {
        BookLookup.Results =
        [
            new BookMetadata
            {
                Source = "openlibrary",
                SourceId = "/books/OL1M",
                Title = "Mort",
                CoverImageUrl = new Uri("https://covers.openlibrary.org/b/id/1-L.jpg"),
            },
        ];
        var gate = new TaskCompletionSource();
        BookLookup.CoverDownloadGate = gate.Task;
        var page = Render<BookEditPage>();
        page.Find("#lookup-query").Input("mort");
        page.Find(".lookup-row button").Click();
        page.WaitForElement(".lookup-results button").Click();

        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.Contains(
            "Downloading cover",
            page.Find(".form-actions [role=status]").TextContent,
            StringComparison.Ordinal));
        Assert.True(page.Find("button[type=submit]").HasAttribute("disabled"));

        gate.SetResult();

        page.WaitForAssertion(() => Assert.Matches("/books/[0-9a-f-]{36}$", Navigation.Uri));
    }

    [Fact]
    public void A_lookup_with_no_results_says_so()
    {
        var page = Render<BookEditPage>();

        page.Find("#lookup-query").Input("nothing");
        page.Find("#lookup-query").KeyDown("Enter");

        page.WaitForAssertion(() => Assert.Contains(
            "Nothing found",
            page.Find(".book-lookup").TextContent,
            StringComparison.Ordinal));
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
    public async Task Series_suggests_existing_names_and_saves_with_a_number()
    {
        Success(await Library.AddBookAsync(new BookInput { Title = "Mort", Series = "Discworld" }, Ct));
        Success(await Library.AddBookAsync(new BookInput { Title = "Dune", Series = "Dune" }, Ct));

        var page = Render<BookEditPage>();
        page.WaitForAssertion(() => Assert.Equal(
            ["Discworld", "Dune"],
            page.FindAll("#series-options option").Select(o => o.GetAttribute("value"))));

        page.Find("#title").Change("Small Gods");
        page.Find("#series").Change("discworld");
        page.Find("#series-number").Change("13.5");
        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.Matches("/books/[0-9a-f-]{36}$", Navigation.Uri));
        var saved = await GetBookAsync(Guid.Parse(Navigation.Uri[^36..]));
        Assert.Equal(("Discworld", 13.5), (saved.Series, saved.SeriesNumber));
    }

    [Fact]
    public void Editing_a_missing_book_says_so()
    {
        var page = Render<BookEditPage>(p => p.Add(x => x.Id, Guid.NewGuid()));

        page.WaitForAssertion(() => Assert.Contains("This book no longer exists.", page.Markup));
    }

    private static BookMetadata LookupResult(string title) =>
        new() { Source = "openlibrary", SourceId = "/works/OL1W", Title = title };

    private static void PickFirstResult(IRenderedComponent<BookEditPage> page, string query)
    {
        page.Find("#lookup-query").Input(query);
        page.Find(".lookup-row button").Click();
        page.WaitForElement(".lookup-results button").Click();
        page.WaitForElement(".book-lookup .alert-success");
    }
}
