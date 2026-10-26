using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Bookside.Core.Books.Lookup;

namespace Bookside.Integrations.Books;

/// <summary>
/// Shared pieces of the book metadata clients.
/// </summary>
internal static partial class BookHttp
{
    public static async Task<string> GetJsonAsync(
        HttpClient http,
        Uri path,
        string source,
        CancellationToken cancellationToken)
    {
        var (status, body) = await LookupHttp
            .GetStringAsync(
                http,
                path,
                ex => Unreachable(source, ex),
                cancellationToken)
            .ConfigureAwait(false);

        return status == HttpStatusCode.OK
            ? body
            : throw new BookLookupException($"{source} returned HTTP {(int)status}.");
    }

    /// <summary>
    /// First four-digit run: "1985", "May 1985", "2004-05-01".
    /// </summary>
    public static int? Year(string? date) =>
        date is not null && YearPattern().Match(date) is { Success: true } m
            ? int.Parse(m.Value, CultureInfo.InvariantCulture)
            : null;

    public static BookLookupException Unexpected(string source, Exception? inner = null) =>
        inner is null
            ? new($"{source} returned an unexpected response.")
            : new($"{source} returned an unexpected response.", inner);

    public static BookLookupException Unreachable(string source, Exception inner) =>
        new($"{source} could not be reached.", inner);

    [GeneratedRegex(@"\d{4}")]
    private static partial Regex YearPattern();
}
