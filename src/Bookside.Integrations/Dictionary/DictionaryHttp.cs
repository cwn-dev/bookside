using System.Net;
using Bookside.Core.Vocabulary.Lookup;

namespace Bookside.Integrations.Dictionary;

/// <summary>
/// Shared GET for dictionary clients: maps transport failures to
/// <see cref="DictionaryLookupException"/>.
/// </summary>
internal static class DictionaryHttp
{
    /// <summary>
    /// Builds <c>entries/{language}/{term}</c> with both segments escaped.
    /// </summary>
    public static Uri EntriesPath(string language, string term) =>
        new(
            $"entries/{Uri.EscapeDataString(language)}/{Uri.EscapeDataString(term)}",
            UriKind.Relative);

    public static Task<(HttpStatusCode Status, string Body)> GetAsync(
        HttpClient http,
        Uri path,
        string source,
        CancellationToken cancellationToken) =>
            LookupHttp.GetStringAsync(
                http,
                path,
                ex => new DictionaryLookupException($"{source} could not be reached.", ex),
                cancellationToken);
}
