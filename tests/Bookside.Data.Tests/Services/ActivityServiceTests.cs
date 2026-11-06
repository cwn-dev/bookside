using Bookside.Core.Activity.Services;
using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.Core.Common;
using Bookside.Core.Vocabulary.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.Data.Tests.Services;

public sealed class ActivityServiceTests : ServiceTestBase
{
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<TimeProvider>(_clock);

    [Fact]
    public async Task Reads_overlapping_the_month_are_listed_with_their_spans()
    {
        var before = await AddBookAsync("Before");
        var across = await AddBookAsync("Across");
        var reading = await AddBookAsync("Reading");
        var stopped = await AddBookAsync("Stopped");
        var past = await AddBookAsync("Past");
        await AddSessionAsync(before.Id, ReadingStatus.Finished, new(2026, 4, 1), new(2026, 5, 31));
        await AddSessionAsync(across.Id, ReadingStatus.Finished, new(2026, 5, 20), new(2026, 6, 3));
        await AddSessionAsync(reading.Id, ReadingStatus.Reading, new(2026, 6, 10), null);
        await AddSessionAsync(stopped.Id, ReadingStatus.Abandoned, new(2026, 6, 12), null);
        await AddSessionAsync(past.Id, ReadingStatus.Finished, null, new(2026, 6, 20), rating: 5);

        var month = Success(await GetAsync(2026, 6));

        Assert.Equal(["Across", "Reading", "Stopped", "Past"], month.Reads.Select(r => r.Title));
        Assert.Equal(
            [(new DateOnly(2026, 5, 20), new DateOnly(2026, 6, 3)),
             (new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 15)),
             (new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 12)),
             (new DateOnly(2026, 6, 20), new DateOnly(2026, 6, 20))],
            month.Reads.Select(r => (r.From, r.To)));
        Assert.Equal(5, month.Reads[3].Rating);
        Assert.Empty(Success(await GetAsync(2026, 7)).Reads); // reading stops at today
    }

    [Fact]
    public async Task Events_are_placed_on_the_local_date()
    {
        _clock.Zone = TimeZoneInfo.CreateCustomTimeZone("plus2", TimeSpan.FromHours(2), "plus2", "plus2");
        // 23:30 UTC on 31 May is 01:30 on 1 June locally.
        _clock.Set(new DateTimeOffset(2026, 5, 31, 23, 30, 0, TimeSpan.Zero));
        var book = await AddBookAsync("Mort");
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "brillig", BookId = book.Id }, Ct));
        Success(await Library.AddCommentAsync(
            book.Id,
            new BookCommentInput { Text = "Loved @[Death] here." },
            Ct));

        var june = Success(await GetAsync(2026, 6));

        Assert.Empty(Success(await GetAsync(2026, 5)).Events);
        Assert.All(june.Events, e => Assert.Equal(new DateOnly(2026, 6, 1), e.Date));
        Assert.Equal(
            [ActivityKind.BookAdded, ActivityKind.WordAdded, ActivityKind.Comment],
            june.Events.Select(e => e.Kind));
        Assert.Equal("brillig", june.Events[1].Text);
        Assert.Equal("Mort", june.Events[1].BookTitle);
        Assert.Equal("Loved @Death here.", june.Events[2].Text);
    }

    [Fact]
    public async Task Flash_cards_started_are_counted_per_day()
    {
        Dictionary.Define("ennui", "Listlessness.");
        Dictionary.Define("brillig", "Teatime.");
        var ennui = Success(await Vocabulary.AddWordAsync(new WordInput { Term = "ennui" }, Ct));
        var brillig = Success(await Vocabulary.AddWordAsync(new WordInput { Term = "brillig" }, Ct));
        Success(await Reviews.RecordReviewAsync(ennui.Id, knew: true, Ct));
        Success(await Reviews.RecordReviewAsync(brillig.Id, knew: false, Ct));

        var cards = Success(await GetAsync(2026, 6)).Events
            .Single(e => e.Kind == ActivityKind.FlashCardsStarted);

        Assert.Equal(new DateOnly(2026, 6, 15), cards.Date);
        Assert.Equal(2, cards.Count);
    }

    [Fact]
    public async Task The_goal_is_reached_on_the_finish_that_hits_the_target()
    {
        var book = await AddBookAsync("Mort");
        Assert.True((await Challenges.SetGoalAsync(2026, 2, Ct)).IsSuccess);
        await AddSessionAsync(book.Id, ReadingStatus.Finished, null, new(2026, 3, 1));
        await AddSessionAsync(book.Id, ReadingStatus.Finished, null, new(2026, 6, 4));
        await AddSessionAsync(book.Id, ReadingStatus.Finished, null, new(2026, 6, 9));

        var goal = Success(await GetAsync(2026, 6)).Events
            .Single(e => e.Kind == ActivityKind.GoalReached);

        Assert.Equal(new DateOnly(2026, 6, 4), goal.Date);
        Assert.Equal(2, goal.Count);
        Assert.DoesNotContain(
            Success(await GetAsync(2026, 3)).Events,
            e => e.Kind == ActivityKind.GoalReached);
    }

    [Fact]
    public async Task Invalid_months_are_validation_errors()
    {
        var error = Failure(await GetAsync(0, 13), ErrorType.Validation);

        Assert.Equal(["Year", "Month"], error.FieldErrors.Select(e => e.Field));
        Success(await GetAsync(9999, 12));
    }

    private Task<Result<ActivityMonth>> GetAsync(int year, int month) =>
        Activity.GetMonthAsync(year, month, Ct);

    private async Task AddSessionAsync(
        Guid bookId,
        ReadingStatus status,
        DateOnly? started,
        DateOnly? finished,
        int? rating = null) =>
        Success(await Library.AddReadingSessionAsync(
            bookId,
            new ReadingSessionInput
            {
                Status = status,
                StartedOn = started,
                FinishedOn = finished,
                Rating = rating,
            },
            Ct));
}
