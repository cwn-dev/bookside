using System.Net;
using System.Text.Json;
using Bookside.Core.Vocabulary;
using Bookside.Core.Vocabulary.Lookup;
using Polly;

namespace Bookside.Integrations.Dictionary;

/// <summary>
/// dictionaryapi.dev client. Typed HttpClient; retries and timeouts come from the resilience handler
/// configured in <see cref="IntegrationsServiceCollectionExtensions.AddDictionaryApiClient"/>.
/// </summary>
internal sealed class DictionaryApiClient(HttpClient http) : IDictionaryLookupService
{
    public const string SourceId = "dictionaryapi.dev";

    public string Source => SourceId;

    public async Task<DictionaryLookupResult> LookupAsync(string term, string language = "en", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        var path = new Uri($"entries/{Uri.EscapeDataString(language)}/{Uri.EscapeDataString(term)}", UriKind.Relative);
        string body;
        try
        {
            using var response = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // The API's "No Definitions Found" reply is a JSON object. Anything else (an HTML 404 from a proxy,
                // a moved route) is treated as a failure so it never gets cached as NotFound.
                return IsJsonObject(body)
                    ? DictionaryLookupResult.NotFound(body)
                    : throw new DictionaryLookupException($"{SourceId} returned 404 without its JSON error body.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new DictionaryLookupException($"{SourceId} returned HTTP {(int)response.StatusCode}.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or ExecutionRejectedException)
        {
            // Network errors, response too large, timeouts (attempt/total), open circuit.
            throw new DictionaryLookupException($"{SourceId} could not be reached.", ex);
        }

        return Parse(body);
    }

    internal static DictionaryLookupResult Parse(string body)
    {
        List<ApiEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize(body, DictionaryApiJsonContext.Default.ListApiEntry);
        }
        catch (JsonException ex)
        {
            throw new DictionaryLookupException($"{SourceId} returned an unexpected response.", ex);
        }

        entries ??= [];
        var meanings = MergeMeanings(entries);
        if (meanings.Count == 0)
        {
            return DictionaryLookupResult.NotFound(body);
        }

        var phonetics = entries.SelectMany(e => e.Phonetics ?? []).ToList();
        return new DictionaryLookupResult
        {
            Status = DictionaryLookupStatus.Found,
            Phonetic = entries.Select(e => e.Phonetic).Concat(phonetics.Select(p => p.Text))
                .Select(p => p?.Trim())
                .FirstOrDefault(p => !string.IsNullOrEmpty(p) && p.Length <= DictionaryEntry.PhoneticMaxLength),
            AudioUrl = phonetics.Select(p => NormalizeAudioUrl(p.Audio)).FirstOrDefault(a => a is not null),
            Meanings = meanings,
            RawResponse = body,
        };
    }

    /// <summary>
    /// The API returns one entry per etymology (e.g. "bank" has four). Merge them into one list grouped by
    /// part of speech, in first-seen order, dropping duplicate and empty definitions.
    /// </summary>
    private static List<DictionaryMeaning> MergeMeanings(List<ApiEntry> entries)
    {
        var result = new List<DictionaryMeaning>();
        foreach (var apiMeaning in entries.SelectMany(e => e.Meanings ?? []))
        {
            var partOfSpeech = apiMeaning.PartOfSpeech?.Trim();
            if (string.IsNullOrEmpty(partOfSpeech))
            {
                partOfSpeech = "unknown";
            }

            var meaning = result.Find(m => string.Equals(m.PartOfSpeech, partOfSpeech, StringComparison.OrdinalIgnoreCase));
            foreach (var apiDefinition in apiMeaning.Definitions ?? [])
            {
                var text = apiDefinition.Definition?.Trim();
                if (string.IsNullOrEmpty(text) || meaning?.Definitions.Exists(d => d.Text == text) == true)
                {
                    continue;
                }

                if (meaning is null)
                {
                    meaning = new DictionaryMeaning { PartOfSpeech = partOfSpeech };
                    result.Add(meaning);
                }

                var example = apiDefinition.Example?.Trim();
                meaning.Definitions.Add(new DictionaryDefinition { Text = text, Example = string.IsNullOrEmpty(example) ? null : example });
            }
        }

        return result;
    }

    private static string? NormalizeAudioUrl(string? audio)
    {
        audio = audio?.Trim();
        if (string.IsNullOrEmpty(audio))
        {
            return null;
        }

        // Older entries use protocol-relative URLs ("//ssl.gstatic.com/...").
        if (audio.StartsWith("//", StringComparison.Ordinal))
        {
            audio = "https:" + audio;
        }

        return audio.Length <= DictionaryEntry.AudioUrlMaxLength
            && Uri.TryCreate(audio, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.AbsoluteUri
            : null;
    }

    private static bool IsJsonObject(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
