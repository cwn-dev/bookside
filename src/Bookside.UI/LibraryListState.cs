using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Bookside.UI;

/// <summary>
/// Remembers the library list a book was opened from, so going back to it
/// (browser Back or the book page's "← Library" link) shows the same books
/// with the opened one focused. One per circuit. It is forgotten as soon as
/// the user goes anywhere other than that list or the opened book's pages.
/// </summary>
public sealed class LibraryListState : IDisposable
{
    private readonly NavigationManager _navigation;
    private string _location;
    private string? _listUrl;
    private int _loadedCount;
    private Guid? _openedBookId;

    public LibraryListState(NavigationManager navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);

        _navigation = navigation;
        _location = navigation.ToBaseRelativePath(navigation.Uri);
        _navigation.LocationChanged += OnLocationChanged;
    }

    /// <summary>
    /// The list the book was opened from, else the plain library.
    /// </summary>
    /// <remarks>
    /// The book page may render before this hears of the navigation to it,
    /// so "still on the list" counts as opened from it.
    /// </remarks>
    public string BackUrlFor(Guid bookId) =>
        _listUrl is not null && (bookId == _openedBookId || _location == _listUrl)
            ? _listUrl
            : "books";

    /// <summary>
    /// Called by the library after each load.
    /// </summary>
    /// <param name="listUrl">Base-relative URL of the list shown.</param>
    internal void Showing(string listUrl, int loadedCount)
    {
        if (listUrl != _listUrl)
        {
            _openedBookId = null;
        }

        _listUrl = listUrl;
        _loadedCount = loadedCount;
    }

    /// <summary>
    /// The books loaded and the book opened when the user left this list for a
    /// book, if they are coming back to it.
    /// </summary>
    internal (int LoadedCount, Guid BookId)? ReturnTo(string listUrl) =>
        listUrl == _listUrl && _openedBookId is { } bookId
            ? (_loadedCount, bookId)
            : null;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        var previous = _location;
        _location = _navigation.ToBaseRelativePath(e.Location);

        if (_location == _listUrl)
        {
            return;
        }

        var bookId = BookIdOf(_location);
        if (bookId is not null && (previous == _listUrl || bookId == _openedBookId))
        {
            _openedBookId = bookId;
        }
        else
        {
            _listUrl = null;
            _openedBookId = null;
        }
    }

    /// <summary>
    /// True for <c>books/…</c> (a book, a new book), i.e. not the list.
    /// </summary>
    /// <param name="location">A base-relative URL.</param>
    internal static bool IsBookPage(string location) =>
        PathOf(location).StartsWith("books/", StringComparison.Ordinal);

    private static ReadOnlySpan<char> PathOf(string location)
    {
        var path = location.AsSpan();
        var end = path.IndexOfAny('?', '#');
        return end >= 0 ? path[..end] : path;
    }

    /// <summary>
    /// The book of <c>books/{id}</c> and its sub-pages (e.g. edit).
    /// </summary>
    private static Guid? BookIdOf(string location)
    {
        if (!IsBookPage(location))
        {
            return null;
        }

        var path = PathOf(location)["books/".Length..];
        var slash = path.IndexOf('/');
        return Guid.TryParse(slash >= 0 ? path[..slash] : path, out var id) ? id : null;
    }

    public void Dispose() => _navigation.LocationChanged -= OnLocationChanged;
}
