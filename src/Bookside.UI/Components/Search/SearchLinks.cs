using Microsoft.AspNetCore.Components;

namespace Bookside.UI.Components.Search;

/// <summary>
/// Where search results link to.
/// </summary>
internal static class SearchLinks
{
    public static string Book(Guid bookId) => $"books/{bookId}";

    public static string Word(Guid wordId) => $"words/{wordId}";

    public static string Tag(NavigationManager navigation, string name) =>
        WithQuery(navigation, "books", "tag", name);

    /// <summary>
    /// A list page searched for <paramref name="query"/>, e.g. <c>books?q=…</c>.
    /// </summary>
    public static string Search(NavigationManager navigation, string page, string query) =>
        WithQuery(navigation, page, "q", query);

    private static string WithQuery(
        NavigationManager navigation,
        string page,
        string name,
        string value) =>
            navigation.GetUriWithQueryParameters(
                navigation.ToAbsoluteUri(page).ToString(),
                new Dictionary<string, object?> { [name] = value });
}
