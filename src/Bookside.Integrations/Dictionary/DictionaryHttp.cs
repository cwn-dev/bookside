using System.Net;
using Bookside.Core.Vocabulary.Lookup;
using Polly;

namespace Bookside.Integrations.Dictionary;

/// <summary>Shared GET for dictionary clients: maps transport failures to <see cref="DictionaryLookupException"/>.</summary>
internal static class DictionaryHttp
{
    /// <summary>Builds <c>entries/{language}/{term}</c> with both segments escaped.</summary>
    public static Uri EntriesPath(string language, string term) =>
        new($"entries/{Uri.EscapeDataString(language)}/{Uri.EscapeDataString(term)}", UriKind.Relative);

    public static async Task<(HttpStatusCode Status, string Body)> GetAsync(
        HttpClient http,
        Uri path,
        string source,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return (response.StatusCode, body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or ExecutionRejectedException)
        {
            // Network errors, response too large, timeouts (attempt/total), open circuit.
            throw new DictionaryLookupException($"{source} could not be reached.", ex);
        }
    }
}
