using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.UI.Pages;

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
    public void Unknown_book_shows_not_found()
    {
        var page = Render<BookDetailsPage>(p => p.Add(x => x.Id, Guid.NewGuid()));

        page.WaitForAssertion(() => Assert.Equal("Book not found", page.Find("h1").TextContent));
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
