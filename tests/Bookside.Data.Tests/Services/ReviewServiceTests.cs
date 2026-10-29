using Bookside.Core.Common;
using Bookside.Core.Vocabulary;
using Bookside.Core.Vocabulary.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.Data.Tests.Services;

public sealed class ReviewServiceTests : ServiceTestBase
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    private readonly TestClock _clock = new(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<TimeProvider>(_clock);

    [Fact]
    public async Task Only_words_with_a_definition_are_reviewed()
    {
        await AddDefinedWordAsync("ennui");
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "brillig" }, Ct));
        Dictionary.Unavailable = true;
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "petrichor" }, Ct));

        var queue = await Reviews.GetQueueAsync(Ct);
        var status = await Reviews.GetStatusAsync(Ct);

        var card = Assert.Single(queue);
        Assert.Equal("ennui", card.Word.Term);
        Assert.True(card.IsNew);
        Assert.Equal(1, card.KnownIntervalDays);
        Assert.Equal("Listlessness.", card.Word.Definition!.Meanings[0].Definitions[0].Text);
        Assert.Equal(new ReviewStatus { NewCount = 1, ReviewableCount = 1 }, status);
    }

    [Fact]
    public async Task Knowing_a_word_schedules_it_and_takes_it_off_todays_queue()
    {
        var word = await AddDefinedWordAsync("ennui");

        var outcome = Success(await Reviews.RecordReviewAsync(word.Id, knew: true, Ct));

        Assert.Equal(new ReviewOutcome { DueOn = Today.AddDays(1), IntervalDays = 1 }, outcome);
        Assert.Empty(await Reviews.GetQueueAsync(Ct));
        Assert.Equal(
            new ReviewStatus { NextDueOn = Today.AddDays(1), ReviewableCount = 1 },
            await Reviews.GetStatusAsync(Ct));

        SetToday(Today.AddDays(1));
        var card = Assert.Single(await Reviews.GetQueueAsync(Ct));
        Assert.False(card.IsNew);
        Assert.Equal(6, card.KnownIntervalDays);
        Assert.Equal(1, (await Reviews.GetStatusAsync(Ct)).DueCount);
    }

    [Fact]
    public async Task Only_the_first_review_of_the_day_counts()
    {
        var word = await AddDefinedWordAsync("ennui");

        Success(await Reviews.RecordReviewAsync(word.Id, knew: false, Ct));
        var again = Success(await Reviews.RecordReviewAsync(word.Id, knew: true, Ct));

        Assert.Equal(new ReviewOutcome { DueOn = Today.AddDays(1), IntervalDays = 1 }, again);
        SetToday(Today.AddDays(1));
        Assert.Equal(1, Assert.Single(await Reviews.GetQueueAsync(Ct)).KnownIntervalDays);
    }

    [Fact]
    public async Task Due_words_come_first_most_overdue_first_then_new_words_oldest_first()
    {
        var a = await AddDefinedWordAsync("a");
        var b = await AddDefinedWordAsync("b");
        Success(await Reviews.RecordReviewAsync(b.Id, knew: true, Ct));
        SetToday(Today.AddDays(1));
        Success(await Reviews.RecordReviewAsync(a.Id, knew: false, Ct));
        await AddDefinedWordAsync("c");
        await AddDefinedWordAsync("d");

        SetToday(Today.AddDays(3));
        var queue = await Reviews.GetQueueAsync(Ct);

        Assert.Equal(["b", "a", "c", "d"], queue.Select(c => c.Word.Term));
        Assert.Equal([false, false, true, true], queue.Select(c => c.IsNew));
    }

    [Fact]
    public async Task New_words_are_limited_per_day()
    {
        var words = new List<WordDetails>();
        for (var i = 0; i < ReviewScheduler.NewWordsPerDay + 3; i++)
        {
            words.Add(await AddDefinedWordAsync($"w{i:00}"));
        }

        Assert.Equal(ReviewScheduler.NewWordsPerDay, (await Reviews.GetQueueAsync(Ct)).Count);
        foreach (var word in words.Take(4))
        {
            Success(await Reviews.RecordReviewAsync(word.Id, knew: true, Ct));
        }

        var status = await Reviews.GetStatusAsync(Ct);
        Assert.Equal(ReviewScheduler.NewWordsPerDay - 4, status.NewCount);
        Assert.Equal(3, status.NewWaitingCount);
        Assert.Equal(ReviewScheduler.NewWordsPerDay - 4, (await Reviews.GetQueueAsync(Ct)).Count);

        SetToday(Today.AddDays(1));
        var tomorrow = await Reviews.GetQueueAsync(Ct);
        Assert.Equal(4, tomorrow.Count(c => !c.IsNew));
        Assert.Equal(words.Count - 4, tomorrow.Count(c => c.IsNew));
    }

    [Fact]
    public async Task Practice_of_forgotten_words_has_only_those_forgotten_today()
    {
        var yesterday = await AddDefinedWordAsync("a");
        var forgotten = await AddDefinedWordAsync("b");
        var known = await AddDefinedWordAsync("c");
        await AddDefinedWordAsync("d");
        Success(await Reviews.RecordReviewAsync(yesterday.Id, knew: false, Ct));
        SetToday(Today.AddDays(1));
        Success(await Reviews.RecordReviewAsync(forgotten.Id, knew: false, Ct));
        Success(await Reviews.RecordReviewAsync(known.Id, knew: true, Ct));

        var cards = await Reviews.GetPracticeCardsAsync(PracticeSet.ForgottenToday, Ct);

        Assert.Equal("b", Assert.Single(cards).Word.Term);
        Assert.Equal(1, (await Reviews.GetStatusAsync(Ct)).ForgottenTodayCount);
    }

    [Fact]
    public async Task Practice_of_most_forgotten_words_puts_the_hardest_first()
    {
        var once = await AddDefinedWordAsync("a");
        var twice = await AddDefinedWordAsync("b");
        var lapsed = await AddDefinedWordAsync("c");
        var known = await AddDefinedWordAsync("d");
        await AddDefinedWordAsync("e");
        Success(await Reviews.RecordReviewAsync(once.Id, knew: false, Ct));
        Success(await Reviews.RecordReviewAsync(twice.Id, knew: false, Ct));
        Success(await Reviews.RecordReviewAsync(lapsed.Id, knew: true, Ct));
        Success(await Reviews.RecordReviewAsync(known.Id, knew: true, Ct));
        SetToday(Today.AddDays(1));
        Success(await Reviews.RecordReviewAsync(twice.Id, knew: false, Ct));
        Success(await Reviews.RecordReviewAsync(lapsed.Id, knew: false, Ct));

        var cards = await Reviews.GetPracticeCardsAsync(PracticeSet.MostForgotten, Ct);

        // c lapsed after being learnt; b's ease dropped twice, a's once.
        Assert.Equal(["c", "b", "a"], cards.Select(c => c.Word.Term));
    }

    [Fact]
    public async Task Random_practice_takes_a_round_of_defined_words()
    {
        for (var i = 0; i < ReviewScheduler.PracticeRoundSize + 5; i++)
        {
            await AddDefinedWordAsync($"w{i:00}");
        }

        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "brillig" }, Ct));

        var cards = await Reviews.GetPracticeCardsAsync(PracticeSet.Random, Ct);

        Assert.Equal(ReviewScheduler.PracticeRoundSize, cards.Count);
        Assert.Equal(cards.Count, cards.Select(c => c.Word.Id).Distinct().Count());
        Assert.DoesNotContain(cards, c => c.Word.Term == "brillig");
        Assert.Equal(
            ReviewScheduler.PracticeRoundSize + 5,
            (await Reviews.GetStatusAsync(Ct)).ReviewableCount);
    }

    [Fact]
    public async Task Unknown_word_is_not_found()
    {
        var error = Failure(
            await Reviews.RecordReviewAsync(Guid.NewGuid(), knew: true, Ct),
            ErrorType.NotFound);

        Assert.Equal(ErrorCodes.WordNotFound, error.Code);
    }

    [Fact]
    public async Task Deleting_a_word_deletes_its_review()
    {
        var word = await AddDefinedWordAsync("ennui");
        Success(await Reviews.RecordReviewAsync(word.Id, knew: true, Ct));

        Assert.True((await Vocabulary.DeleteWordAsync(word.Id, Ct)).IsSuccess);

        await using var db = Database.CreateContext();
        Assert.False(await db.WordReviews.AnyAsync(Ct));
    }

    [Fact]
    public async Task Home_summary_includes_the_review_status()
    {
        await AddDefinedWordAsync("ennui");

        var home = await Home.GetHomeAsync(Ct);

        Assert.Equal(new ReviewStatus { NewCount = 1, ReviewableCount = 1 }, home.Review);
    }

    private async Task<WordDetails> AddDefinedWordAsync(string term)
    {
        Dictionary.Define(term, term == "ennui" ? "Listlessness." : $"Meaning of {term}.");
        return Success(await Vocabulary.AddWordAsync(new WordInput { Term = term }, Ct));
    }

    private void SetToday(DateOnly date) =>
        _clock.Set(new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));
}
