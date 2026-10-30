using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.Core.Challenge.Services;
using System.Globalization;
using Bookside.UI.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.UI.Tests.Components;

public sealed class ChallengePageTests : ComponentTestBase
{
    private static int ThisYear => TimeProvider.System.GetLocalNow().Year;

    private IChallengeService Challenges => Services.GetRequiredService<IChallengeService>();

    [Fact]
    public async Task Books_are_grouped_by_month_newest_first()
    {
        var mort = await AddBookAsync("Mort");
        var eric = await AddBookAsync("Eric");
        await FinishAsync(mort.Id, new DateOnly(2024, 3, 2));
        await FinishAsync(eric.Id, new DateOnly(2024, 7, 9));
        await FinishAsync(mort.Id, new DateOnly(2024, 7, 1));

        var page = Render<ChallengePage>(p => p.Add(c => c.Year, 2024));

        page.WaitForAssertion(() => page.Find(".month"));
        Assert.Equal(["July 2", "March 1"], page.FindAll(".month h3").Select(h => h.TextContent));
        Assert.Equal(["Eric", "Mort", "Mort"], page.FindAll(".book-card .title").Select(t => t.TextContent));
        Assert.Equal(["2024", ThisYear.ToString(CultureInfo.InvariantCulture)],
            page.FindAll(".year-name").Select(y => y.TextContent).Order());
    }

    [Fact]
    public void Without_a_year_it_opens_the_current_year_and_sets_a_goal()
    {
        var page = Render<ChallengePage>();
        page.WaitForAssertion(() => page.Find(".progress form"));
        Assert.Equal($"{ThisYear} reading challenge", page.Find("h1").TextContent);

        page.Find(".progress input").Change("30");
        page.Find(".progress form").Submit();

        page.WaitForAssertion(() => page.Find(".progress [role=progressbar]"));
        Assert.Contains("0 of 30 books", page.Find(".progress-count").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_goal_can_be_changed_and_removed()
    {
        Assert.True((await Challenges.SetGoalAsync(2024, 10, Ct)).IsSuccess);
        var page = Render<ChallengePage>(p => p.Add(c => c.Year, 2024));
        page.WaitForAssertion(() => page.Find(".progress-footer"));
        Assert.Equal("10 books short of the goal", page.Find(".progress-status").TextContent.Trim());

        page.FindAll("button").Single(b => b.TextContent == "Edit goal").Click();
        page.Find(".progress input").Change("0");
        page.Find(".progress form").Submit();
        page.WaitForAssertion(() => Assert.Contains("Enter a number", page.Find(".progress").TextContent, StringComparison.Ordinal));

        page.FindAll("button").Single(b => b.TextContent == "Remove goal").Click();
        page.FindAll("button").Single(b => b.TextContent == "Remove").Click();

        page.WaitForAssertion(() => Assert.Contains("Set a goal for 2024", page.Find(".progress").TextContent, StringComparison.Ordinal));
        Assert.Null(Success(await Challenges.GetYearAsync(2024, Ct)).TargetBooks);
    }

    [Fact]
    public async Task The_year_picker_lists_years_with_reads_and_jumps()
    {
        await FinishAsync((await AddBookAsync("Mort")).Id, new DateOnly(2010, 5, 1));
        var page = Render<ChallengePage>();
        page.WaitForAssertion(() => page.Find(".year-nav select"));
        var options = page.FindAll(".year-nav option")
            .Select(o => int.Parse(o.TextContent, CultureInfo.InvariantCulture))
            .ToList();
        Assert.Equal([ThisYear, 2010], options);
        Assert.Equal(ThisYear.ToString(CultureInfo.InvariantCulture), page.Find(".year-nav option[selected]").TextContent);

        page.Find(".year-nav select").Change("2010");

        Assert.EndsWith("/challenge/2010", Navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_reached_goal_celebrates()
    {
        await FinishAsync((await AddBookAsync("Mort")).Id, new DateOnly(2024, 5, 1));
        Assert.True((await Challenges.SetGoalAsync(2024, 2, Ct)).IsSuccess);
        Assert.True((await Challenges.SetGoalAsync(2025, 1, Ct)).IsSuccess);

        var missed = Render<ChallengePage>(p => p.Add(c => c.Year, 2025));
        missed.WaitForAssertion(() => missed.Find(".progress-footer"));
        Assert.Empty(missed.FindAll(".celebration"));

        var page = Render<ChallengePage>(p => p.Add(c => c.Year, 2024));
        page.WaitForAssertion(() => page.Find(".progress-footer"));
        Assert.Empty(page.FindAll(".celebration"));

        Assert.True((await Challenges.SetGoalAsync(2024, 1, Ct)).IsSuccess);
        var reached = Render<ChallengePage>(p => p.Add(c => c.Year, 2024));
        reached.WaitForAssertion(() => reached.Find(".progress .celebration"));
    }

    private async Task FinishAsync(Guid bookId, DateOnly finishedOn) =>
        Success(await Library.AddReadingSessionAsync(
            bookId,
            new ReadingSessionInput { Status = ReadingStatus.Finished, FinishedOn = finishedOn },
            Ct));
}
