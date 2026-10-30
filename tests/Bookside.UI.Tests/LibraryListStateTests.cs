using Microsoft.AspNetCore.Components;

namespace Bookside.UI.Tests;

public sealed class LibraryListStateTests
{
    private const string List = "books?q=du&sort=recentlyadded";
    private static readonly Guid BookId = Guid.CreateVersion7();
    private readonly TestNavigationManager _navigation = new();

    [Fact]
    public void Remembers_the_list_and_the_book_opened_from_it()
    {
        using var state = ShowList(loadedCount: 96);

        _navigation.Go($"books/{BookId}");

        Assert.Equal(List, state.BackUrlFor(BookId));
        Assert.Equal((96, BookId), state.ReturnTo(List));
    }

    [Fact]
    public void Back_url_is_right_before_it_hears_of_the_navigation()
    {
        using var state = ShowList(loadedCount: 48);

        Assert.Equal(List, state.BackUrlFor(BookId));
    }

    [Fact]
    public void Keeps_it_through_the_book_s_own_pages()
    {
        using var state = ShowList(loadedCount: 48);

        _navigation.Go($"books/{BookId}");
        _navigation.Go($"books/{BookId}/edit");
        _navigation.Go($"books/{BookId}");
        _navigation.Go(List);

        Assert.Equal((48, BookId), state.ReturnTo(List));
    }

    [Fact]
    public void Forgets_it_after_going_elsewhere()
    {
        using var state = ShowList(loadedCount: 48);

        _navigation.Go($"books/{BookId}");
        _navigation.Go("words");

        Assert.Equal("books", state.BackUrlFor(BookId));
        Assert.Null(state.ReturnTo(List));
    }

    [Fact]
    public void A_book_not_opened_from_the_list_goes_back_to_the_whole_library()
    {
        using var state = ShowList(loadedCount: 48);

        _navigation.Go("words");
        _navigation.Go($"books/{BookId}");

        Assert.Equal("books", state.BackUrlFor(BookId));
    }

    [Fact]
    public void Only_returns_to_the_same_list()
    {
        using var state = ShowList(loadedCount: 48);

        _navigation.Go($"books/{BookId}");

        Assert.Null(state.ReturnTo("books"));
    }

    private LibraryListState ShowList(int loadedCount)
    {
        _navigation.Go(List);
        var state = new LibraryListState(_navigation);
        state.Showing(List, loadedCount);
        return state;
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/");

        public void Go(string relativeUrl) => NavigateTo(relativeUrl);

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Uri = ToAbsoluteUri(uri).ToString();
            NotifyLocationChanged(isInterceptedLink: false);
        }
    }
}
