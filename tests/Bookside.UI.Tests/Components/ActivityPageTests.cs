using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.UI.Pages;

namespace Bookside.UI.Tests.Components;

public sealed class ActivityPageTests : ComponentTestBase
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);

    [Fact]
    public async Task This_month_shows_reads_as_bars_and_lists_today()
    {
        var book = await AddBookAsync("Mort");
        Success(await Library.AddReadingSessionAsync(
            book.Id,
            new ReadingSessionInput { Status = ReadingStatus.Reading, StartedOn = Today },
            Ct));
        await AddWordAsync("brillig", book.Id);

        var page = Render<ActivityPage>();

        page.WaitForAssertion(() => page.Find(".bar"));
        Assert.Equal($"books/{book.Id}", page.Find(".bar.reading").GetAttribute("href"));
        Assert.Equal("Mort, reading", page.Find(".bar").GetAttribute("aria-label"));
        // The book's word is a mark on its line, not a pill; "Added" is
        // the line itself.
        Assert.Equal("Word: brillig (Mort)", page.Find(".bar .mark").GetAttribute("title"));
        Assert.True(page.Find(".bar .mark .activity-icon").ClassList.Contains("word"));
        Assert.Empty(page.FindAll(".pill"));
        // The mark is on the bar's first day, so the title goes after it.
        Assert.Contains("--lead:", page.Find(".bar").GetAttribute("style"));
        Assert.Equal(
            ["Started", "Added", "Word"],
            page.FindAll(".day-items .day-label").Select(l => l.TextContent.Trim()));
    }

    [Fact]
    public async Task Clicking_a_day_lists_its_items()
    {
        var book = await AddBookAsync("Eric");
        Success(await Library.AddReadingSessionAsync(
            book.Id,
            new ReadingSessionInput
            {
                Status = ReadingStatus.Finished,
                StartedOn = new DateOnly(2024, 3, 2),
                FinishedOn = new DateOnly(2024, 3, 9),
                Rating = 3,
            },
            Ct));

        var page = Render<ActivityPage>(p => p.Add(c => c.Year, 2024).Add(c => c.Month, 3));
        page.WaitForAssertion(() => page.Find(".bar.finished"));
        Assert.Equal("March 2024", page.Find("h1").TextContent);
        Assert.Empty(page.FindAll(".day")); // not this month: nothing selected

        page.FindAll("button.day-number").Single(b => b.TextContent.Trim() == "9").Click();

        Assert.Equal("Saturday 9 March", page.Find(".day h2").TextContent);
        Assert.Equal("Eric ★★★", page.Find(".day-items a").TextContent);
        Assert.Equal(2, page.FindAll(".bar").Count); // split across two weeks
    }

    [Fact]
    public async Task A_busy_day_shows_some_icons_and_opens_the_rest()
    {
        for (var i = 0; i < 14; i++)
        {
            await AddBookAsync($"Book {i}");
        }

        var page = Render<ActivityPage>();
        page.WaitForAssertion(() => page.Find(".more-icons"));

        Assert.Equal(11, page.FindAll(".pills > .pill").Count);
        Assert.Equal("▾ +3", page.Find(".more-icons").TextContent.Trim());
        Assert.Empty(page.FindAll(".pills-popover"));

        page.Find(".more-icons").Click();

        Assert.Equal(14, page.FindAll(".pills-popover .pill").Count);
        Assert.Equal("true", page.Find(".pills > .more-icons").GetAttribute("aria-expanded"));

        page.Find(".pills-popover .more-icons").Click();

        Assert.Empty(page.FindAll(".pills-popover"));

        // A click anywhere else closes it too.
        page.Find(".more-icons").Click();
        page.Find(".popover-backdrop").Click();

        Assert.Empty(page.FindAll(".pills-popover"));
    }

    [Fact]
    public void An_invalid_month_says_so()
    {
        var page = Render<ActivityPage>(p => p.Add(c => c.Year, 2024).Add(c => c.Month, 13));

        page.WaitForAssertion(() => Assert.Equal("No such month", page.Find("h1").TextContent));
    }
}
