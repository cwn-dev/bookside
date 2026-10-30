using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.Core.Home.Services;
using Bookside.UI.Components;

namespace Bookside.UI.Tests.Components;

public sealed class FinishedPopupTests : ComponentTestBase
{
    [Fact]
    public async Task Closes_itself_after_the_duration()
    {
        var closed = new TaskCompletionSource();
        var popup = Render<FinishedPopup>(p => p
            .Add(x => x.Read, Read())
            .Add(x => x.Duration, TimeSpan.FromMilliseconds(50))
            .Add(x => x.OnClose, () => closed.TrySetResult()));

        Assert.NotEmpty(popup.FindAll(".finished-popup"));
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }

    [Fact]
    public void Clicking_the_backdrop_closes_it()
    {
        var closes = 0;
        var popup = Render<FinishedPopup>(p => p
            .Add(x => x.Read, Read())
            .Add(x => x.OnClose, () => closes++));

        popup.Find(".finished-backdrop").Click();
        Assert.Equal(1, closes);
    }

    [Fact]
    public void Renders_nothing_without_a_book() =>
        Assert.Empty(Render<FinishedPopup>().Markup.Trim());

    private static BookRead Read() => new()
    {
        BookId = Guid.CreateVersion7(),
        Title = "Dune",
        Authors = ["Frank Herbert"],
        Session = new ReadingSessionDto
        {
            Id = Guid.CreateVersion7(),
            BookId = Guid.CreateVersion7(),
            Status = ReadingStatus.Finished,
            StartedOn = new DateOnly(2026, 10, 3),
            FinishedOn = new DateOnly(2026, 10, 30),
        },
    };
}
