using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.UI.Pages;
using Microsoft.AspNetCore.Components.Web;

namespace Bookside.UI.Tests.Components;

public sealed class BookDetailsPageTests : ComponentTestBase
{
    [Fact]
    public async Task Shows_book_details()
    {
        var book = Success(await Library.AddBookAsync(
            new BookInput { Title = "Dune", Subtitle = "Book one", Authors = ["Frank Herbert"], PageCount = 412 },
            Ct));

        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, book.Id));

        page.WaitForAssertion(() => Assert.Equal("Dune", page.Find("h1").TextContent));
        Assert.Contains("Book one", page.Find(".subtitle").TextContent);
        Assert.Contains("Frank Herbert", page.Find(".authors").TextContent);
        Assert.Contains("412", page.Find(".facts").TextContent);
        Assert.Contains("Nothing here yet.", page.Markup);
    }

    [Fact]
    public async Task Series_links_to_a_library_search_for_it()
    {
        var book = Success(await Library.AddBookAsync(new BookInput { Title = "Mort", Series = "Discworld & Co", SeriesNumber = 4 }, Ct));

        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, book.Id));

        page.WaitForAssertion(() => Assert.Equal("Discworld & Co #4", page.Find(".series a").TextContent));
        Assert.Equal("books?q=Discworld%20%26%20Co", page.Find(".series a").GetAttribute("href"));
    }

    [Fact]
    public void Unknown_book_shows_not_found()
    {
        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, Guid.NewGuid()));

        page.WaitForAssertion(() => Assert.Equal("Book not found", page.Find("h1").TextContent));
    }

    [Fact]
    public async Task Tags_are_added_toggled_and_link_to_the_filtered_library()
    {
        var book = await AddBookAsync("Mort");
        var other = await AddBookAsync("Dune");
        Success(await Tags.SetBookTagsAsync(other.Id, ["Own"], Ct));
        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, book.Id));
        page.WaitForAssertion(() => page.Find(".book-tags"));
        Assert.Empty(page.FindAll(".book-tags a.tag"));

        page.FindAll(".book-tags button").Single(b => b.TextContent == "+ Add tags").Click();
        page.WaitForAssertion(() => page.Find(".tag-editor"));
        page.Find(".tag-editor .new-tag input").Change("Have e-book");
        page.Find(".tag-editor .new-tag").Submit();
        page.WaitForAssertion(() => Assert.Equal(["Have e-book", "Own"], page.FindAll(".toggles .chip").Select(c => c.TextContent)));
        page.FindAll(".toggles .chip").Single(c => c.TextContent == "Own").Click();

        page.WaitForAssertion(() => Assert.Equal(
            ["true", "true"],
            page.FindAll(".toggles .chip").Select(c => c.GetAttribute("aria-pressed"))));
        Assert.Equal("", page.Find(".tag-editor .new-tag input").GetAttribute("value") ?? "");
        Assert.Equal(["Have e-book", "Own"], (await GetBookAsync(book.Id)).Tags);

        page.FindAll(".toggles .chip").Single(c => c.TextContent == "Have e-book").Click();
        page.WaitForAssertion(() => Assert.Equal(["Own"], page.FindAll(".toggles .chip.selected").Select(c => c.TextContent)));
        page.FindAll(".tag-editor button").Single(b => b.TextContent == "Done").Click();

        var link = page.Find(".book-tags a.tag");
        Assert.Equal("Own", link.TextContent);
        Assert.Equal("books?tag=Own", link.GetAttribute("href"));
        Assert.Equal("Edit tags", page.Find(".book-tags button").TextContent);
        Assert.Equal(["Own"], (await GetBookAsync(book.Id)).Tags);
    }

    [Fact]
    public async Task Too_long_new_tag_shows_the_error()
    {
        var book = await AddBookAsync("Mort");
        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, book.Id));
        page.WaitForAssertion(() => page.Find(".book-tags button"));
        page.Find(".book-tags button").Click();

        page.WaitForAssertion(() => page.Find(".tag-editor"));
        page.Find(".tag-editor .new-tag input").Change(new string('x', Tag.NameMaxLength + 1));
        page.Find(".tag-editor .new-tag").Submit();

        page.WaitForAssertion(() => Assert.Contains("at most 50 characters", page.Find(".tag-editor .alert").TextContent, StringComparison.Ordinal));
        Assert.Empty((await GetBookAsync(book.Id)).Tags);
    }

    [Fact]
    public async Task Start_reading_then_finish_with_a_rating()
    {
        var book = await AddBookAsync("Dune");
        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, book.Id));
        page.WaitForAssertion(() => page.Find("h1"));

        page.FindAll("button").Single(b => b.TextContent == "Start reading").Click();
        page.WaitForAssertion(() => Assert.Contains("Reading now", page.Find(".current").TextContent));

        page.FindAll(".current button").Single(b => b.TextContent == "Finished").Click();
        page.FindAll(".rating-input .star")[3].Click();
        page.Find(".editor form").Submit();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".current")));
        var session = Assert.Single((await GetBookAsync(book.Id)).ReadingSessions);
        Assert.Equal(ReadingStatus.Finished, session.Status);
        Assert.Equal(4, session.Rating);
        Assert.NotNull(session.StartedOn);
        Assert.Equal(session.StartedOn, session.FinishedOn);
        Assert.Contains("Finished", page.Find(".session .badge").TextContent);
        Assert.Contains("Dune", page.Find(".finished-popup").TextContent);
    }

    [Fact]
    public async Task Logging_a_past_read_shows_no_finished_popup()
    {
        var book = await AddBookAsync("Dune");
        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, book.Id));
        page.WaitForAssertion(() => page.Find("h1"));

        page.FindAll("button").Single(b => b.TextContent == "Log a past read").Click();
        page.Find(".editor form").Submit();

        page.WaitForAssertion(() => page.Find(".session"));
        Assert.Empty(page.FindAll(".finished-popup"));
    }

    [Fact]
    public async Task Session_form_shows_date_order_error_from_the_service()
    {
        var book = await AddBookAsync("Dune");
        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, book.Id));
        page.WaitForAssertion(() => page.Find("h1"));

        page.FindAll("button").Single(b => b.TextContent == "Log a past read").Click();
        var dates = page.FindAll(".editor input[type=date]");
        dates[0].Change("2026-05-10");
        page.FindAll(".editor input[type=date]")[1].Change("2026-05-01");
        page.Find(".editor form").Submit();

        page.WaitForAssertion(() => Assert.Contains("Cannot be before the start date.", page.Find(".editor .validation-message").TextContent));
        Assert.Empty((await GetBookAsync(book.Id)).ReadingSessions);
    }

    [Fact]
    public async Task Deleting_the_book_asks_first_then_returns_to_the_library()
    {
        var book = await AddBookAsync("Dune");
        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, book.Id));
        page.WaitForAssertion(() => page.Find("h1"));

        page.FindAll("button").Single(b => b.TextContent == "Delete book").Click();
        Assert.Contains("Delete this book", page.Markup);
        page.Find(".confirm .btn-solid").Click();

        page.WaitForAssertion(() => Assert.EndsWith("/books", Navigation.Uri, StringComparison.Ordinal));
        Assert.False((await Library.GetBookAsync(book.Id, Ct)).IsSuccess);
    }

    [Fact]
    public async Task Add_word_saves_it_to_the_book_and_links_the_count()
    {
        var book = await AddBookAsync("Dune");
        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, book.Id));
        page.WaitForAssertion(() => page.Find("h1"));

        page.FindAll("button").Single(b => b.TextContent == "Add word").Click();
        page.Find(".add-word input[id$='-term']").Change("kanly");
        page.Find(".add-word input[id$='-page']").Change("88");
        page.Find(".add-word form").Submit();

        page.WaitForAssertion(() => Assert.Contains("kanly", page.Find(".word-added").TextContent));
        var count = page.FindAll(".facts div").Single(d => d.TextContent.StartsWith("Words saved", StringComparison.Ordinal));
        Assert.Equal($"words?book={book.Id}", count.QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("1", count.QuerySelector("a")!.TextContent);
        Assert.Equal("", page.Find(".add-word input[id$='-term']").GetAttribute("value") ?? "");
        Assert.Equal("88", page.Find(".add-word input[id$='-page']").GetAttribute("value")); // kept for the next word

        var word = await Vocabulary.FindWordAsync("kanly", cancellationToken: Ct);
        Assert.Equal(book.Id, word?.BookId);
    }

    [Fact]
    public async Task Posting_a_comment_adds_it_to_the_log_and_clears_the_box()
    {
        var book = await AddBookAsync("Dune");
        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, book.Id));
        page.WaitForAssertion(() => page.Find("h1"));

        page.Find(".add-comment textarea").Change("Spice must flow.\nSecond line.");
        page.Find(".add-comment form").Submit();

        page.WaitForAssertion(() => Assert.Equal("Spice must flow.\nSecond line.", page.Find(".comment .comment-text").TextContent));
        Assert.Equal("", page.Find(".add-comment textarea").GetAttribute("value") ?? "");
        Assert.Empty(page.FindAll(".comment .edited"));
        Assert.Equal("Spice must flow.\nSecond line.", Assert.Single((await GetBookAsync(book.Id)).Comments).Text);
    }

    [Fact]
    public async Task Ctrl_Enter_posts_the_typed_comment()
    {
        var book = await AddBookAsync("Dune");
        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, book.Id));
        page.WaitForAssertion(() => page.Find("h1"));

        // No change event: the text box still has focus.
        var box = page.Find(".add-comment textarea");
        box.Input("Spice must flow.");
        box.KeyDown(new KeyboardEventArgs { Key = "Enter", CtrlKey = true });

        page.WaitForAssertion(() => Assert.Equal(
            "Spice must flow.",
            page.Find(".comment .comment-text").TextContent));
    }

    [Fact]
    public async Task Blank_comment_shows_the_service_validation_error()
    {
        var book = await AddBookAsync("Dune");
        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, book.Id));
        page.WaitForAssertion(() => page.Find("h1"));

        page.Find(".add-comment textarea").Change("   ");
        page.Find(".add-comment form").Submit();

        page.WaitForAssertion(() => Assert.Equal("Required.", page.Find(".add-comment .validation-message").TextContent));
        Assert.Empty((await GetBookAsync(book.Id)).Comments);
    }

    [Fact]
    public async Task Editing_a_comment_marks_it_edited_and_delete_asks_first()
    {
        var book = await AddBookAsync("Dune");
        Success(await Library.AddCommentAsync(book.Id, new BookCommentInput { Text = "Slow start" }, Ct));
        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, book.Id));
        page.WaitForAssertion(() => page.Find(".comment"));

        page.FindAll(".comment button").Single(b => b.TextContent == "Edit").Click();
        page.Find(".comment textarea").Change("Slow start, great finish");
        page.Find(".comment form").Submit();

        page.WaitForAssertion(() => Assert.Equal("Slow start, great finish", page.Find(".comment .comment-text").TextContent));
        Assert.Contains("edited", page.Find(".comment .edited").TextContent, StringComparison.Ordinal);

        page.FindAll(".comment button").Single(b => b.TextContent == "Delete").Click();
        Assert.Contains("Delete this comment?", page.Find(".comment").TextContent, StringComparison.Ordinal);
        page.Find(".comment .confirm .btn-solid").Click();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".comment")));
        Assert.Empty((await GetBookAsync(book.Id)).Comments);
    }

    [Fact]
    public async Task Comments_and_sessions_share_one_newest_first_log()
    {
        var book = await AddBookAsync("Dune");
        Success(await Library.AddReadingSessionAsync(
            book.Id,
            new ReadingSessionInput { Status = ReadingStatus.Finished, StartedOn = new(2020, 3, 1), FinishedOn = new(2020, 3, 30) },
            Ct));
        Success(await Library.AddCommentAsync(book.Id, new BookCommentInput { Text = "Want to re-read" }, Ct));

        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, book.Id));

        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll(".timeline > .entry").Count));
        var classes = page.FindAll(".timeline > .entry").Select(e => e.ClassName).ToList();
        Assert.Equal(["entry comment", "entry session", "entry started"], classes);
        Assert.StartsWith("Started reading", page.Find(".started").TextContent.Trim(), StringComparison.Ordinal);
    }
}
