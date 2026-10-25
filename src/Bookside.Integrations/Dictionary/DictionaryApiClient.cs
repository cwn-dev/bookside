using System.Net;
using System.Text.Json;
using Bookside.Core.Vocabulary;
using Bookside.Core.Vocabulary.Lookup;

namespace Bookside.Integrations.Dictionary;

/// <summary>
/// dictionaryapi.dev client. Typed HttpClient; retries and timeouts come from
/// the resilience handler configured in
/// <see cref="IntegrationsServiceCollectionExtensions.AddDictionaryApiClient"/>.
/// </summary>
internal sealed class DictionaryApiClient(HttpClient http) : IDictionaryLookupService
{
    public const string SourceId = "dictionaryapi.dev";

    public string Source => SourceId;

    public async Task<DictionaryLookupResult> LookupAsync(
        string term,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        var (status, body) = await DictionaryHttp
            .GetAsync(
                http,
                DictionaryHttp.EntriesPath(language, term),
                SourceId,
                cancellationToken)
            .ConfigureAwait(false);

        if (status == HttpStatusCode.NotFound)
        {
            // The API's "No Definitions Found" reply is a JSON object.
            // Anything else (an HTML 404 from a proxy, a moved route) is
            // treated as a failure so it never gets cached as NotFound.
            return IsJsonObject(body)
                ? DictionaryLookupResult.NotFound(body)
                : throw new DictionaryLookupException(
                    $"{SourceId} returned 404 without its JSON error body.");
        }

        if ((int)status is < 200 or > 299)
        {
            throw new DictionaryLookupException(
                $"{SourceId} returned HTTP {(int)status}.");
        }

        return Parse(body);
    }

    internal static DictionaryLookupResult Parse(string body)
    {
        List<ApiEntry>? entries;

        try
        {
            entries = JsonSerializer.Deserialize(
                body,
                DictionaryApiJsonContext.Default.ListApiEntry);
        }
        catch (JsonException ex)
        {
            throw new DictionaryLookupException(
                $"{SourceId} returned an unexpected response.", ex);
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
            Phonetic = MeaningListBuilder.FirstPhonetic(entries.Select(e => e.Phonetic).Concat(phonetics.Select(p => p.Text))),
            AudioUrl = phonetics.Select(p => NormalizeAudioUrl(p.Audio)).FirstOrDefault(a => a is not null),
            Meanings = meanings,
            RawResponse = body,
        };
    }

    /// <summary>
    /// The API returns one entry per etymology (e.g. "bank" has four); they are
    /// merged into one list.
    /// </summary>
    private static List<DictionaryMeaning> MergeMeanings(List<ApiEntry> entries)
    {
        var builder = new MeaningListBuilder();

        foreach (var apiMeaning in entries.SelectMany(e => e.Meanings ?? []))
        {
            foreach (var apiDefinition in apiMeaning.Definitions ?? [])
            {
                builder.Add(
                    apiMeaning.PartOfSpeech,
                    apiDefinition.Definition,
                    apiDefinition.Example);
            }
        }

        return builder.Build();
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
            && (uri.Scheme == Uri.UriSchemeHttps
                || uri.Scheme == Uri.UriSchemeHttp)
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
