using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.Core.Challenge.Services;
using Bookside.Core.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.Data.Tests.Services;

public sealed class ChallengeServiceTests : ServiceTestBase
{
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<TimeProvider>(_clock);

    [Fact]
    public async Task A_year_lists_its_finished_reads_latest_first_including_rereads()
    {
        var mort = await AddBookAsync("Mort", "Terry Pratchett");
        var eric = await AddBookAsync("Eric");
        await AddSessionAsync(mort.Id, ReadingStatus.Finished, new(2026, 1, 1), rating: 4);
        await AddSessionAsync(eric.Id, ReadingStatus.Finished, new(2026, 3, 1));
        await AddSessionAsync(mort.Id, ReadingStatus.Finished, new(2026, 12, 31));

        var year = Success(await Challenges.GetYearAsync(2026, Ct));

        Assert.Equal(2026, year.Year);
        Assert.Equal(new DateOnly(2026, 6, 15), year.Today);
        Assert.Null(year.TargetBooks);
        Assert.Equal(["Mort", "Eric", "Mort"], year.Reads.Select(r => r.Title));
        Assert.Equal(
            [new DateOnly(2026, 12, 31), new DateOnly(2026, 3, 1), new DateOnly(2026, 1, 1)],
            year.Reads.Select(r => r.Session.FinishedOn));
        Assert.Equal(["Terry Pratchett"], year.Reads[0].Authors);
        Assert.Equal(4, year.Reads[2].Session.Rating);
    }

    [Fact]
    public async Task Only_finished_reads_dated_in_the_year_count()
    {
        var book = await AddBookAsync("Mort");
        await AddSessionAsync(book.Id, ReadingStatus.Finished, new(2025, 12, 31));
        await AddSessionAsync(book.Id, ReadingStatus.Finished, new(2027, 1, 1));
        await AddSessionAsync(book.Id, ReadingStatus.Finished, finished: null);
        await AddSessionAsync(book.Id, ReadingStatus.Abandoned, new(2026, 2, 1));
        await AddSessionAsync(book.Id, ReadingStatus.Reading, finished: null);

        var year = Success(await Challenges.GetYearAsync(2026, Ct));

        Assert.Empty(year.Reads);
    }

    [Fact]
    public async Task Setting_a_goal_again_replaces_it()
    {
        Assert.True((await Challenges.SetGoalAsync(2026, 50, Ct)).IsSuccess);
        Assert.True((await Challenges.SetGoalAsync(2026, 40, Ct)).IsSuccess);

        var year = Success(await Challenges.GetYearAsync(2026, Ct));

        Assert.Equal(40, year.TargetBooks);
    }

    [Fact]
    public async Task Removing_a_goal_clears_it()
    {
        Assert.True((await Challenges.SetGoalAsync(2026, 50, Ct)).IsSuccess);

        Assert.True((await Challenges.RemoveGoalAsync(2026, Ct)).IsSuccess);

        Assert.Null(Success(await Challenges.GetYearAsync(2026, Ct)).TargetBooks);
        var error = Failure(await Challenges.RemoveGoalAsync(2026, Ct), ErrorType.NotFound);
        Assert.Equal(ErrorCodes.GoalNotFound, error.Code);
    }

    [Fact]
    public async Task Invalid_goals_return_every_field_error()
    {
        var error = Failure(await Challenges.SetGoalAsync(0, 1001, Ct), ErrorType.Validation);

        Assert.Equal(["Year", "TargetBooks"], error.FieldErrors.Select(e => e.Field));
        Failure(await Challenges.SetGoalAsync(2026, 0, Ct), ErrorType.Validation);
        Failure(await Challenges.GetYearAsync(10_000, Ct), ErrorType.Validation);
    }

    [Fact]
    public async Task Years_list_every_year_with_reads_or_a_goal_and_the_current_year()
    {
        var book = await AddBookAsync("Mort");
        await AddSessionAsync(book.Id, ReadingStatus.Finished, new(2024, 5, 1));
        await AddSessionAsync(book.Id, ReadingStatus.Finished, new(2024, 6, 1));
        await AddSessionAsync(book.Id, ReadingStatus.Abandoned, new(2023, 6, 1));
        Assert.True((await Challenges.SetGoalAsync(2024, 3, Ct)).IsSuccess);
        Assert.True((await Challenges.SetGoalAsync(2027, 20, Ct)).IsSuccess);

        var year = Success(await Challenges.GetYearAsync(2024, Ct));

        Assert.Equal(3, year.TargetBooks);
        Assert.Equal(
            [
                new ChallengeProgress { Year = 2027, ReadCount = 0, TargetBooks = 20 },
                new ChallengeProgress { Year = 2026, ReadCount = 0 },
                new ChallengeProgress { Year = 2024, ReadCount = 2, TargetBooks = 3 },
            ],
            year.Years);
    }

    private async Task AddSessionAsync(
        Guid bookId,
        ReadingStatus status,
        DateOnly? finished,
        int? rating = null) =>
        Success(await Library.AddReadingSessionAsync(
            bookId,
            new ReadingSessionInput
            {
                Status = status,
                FinishedOn = finished,
                Rating = rating,
            },
            Ct));
}
