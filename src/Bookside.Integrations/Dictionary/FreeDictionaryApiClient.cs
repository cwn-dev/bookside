using System.Net;
using System.Text.Json;
using Bookside.Core.Vocabulary;
using Bookside.Core.Vocabulary.Lookup;

namespace Bookside.Integrations.Dictionary;

/// <summary>
/// freedictionaryapi.com client (Wiktionary data, CC BY-SA 4.0).
/// Typed HttpClient; retries and timeouts come from the resilience handler
/// configured in <see cref="IntegrationsServiceCollectionExtensions.AddFreeDictionaryApiClient"/>.
/// </summary>
internal sealed class FreeDictionaryApiClient(HttpClient http) : IDictionaryLookupService
{
    public const string SourceId = "freedictionaryapi.com";

    /// <summary>
    /// Quotes are only used as an example when a sense has none, 
    /// and only short ones.
    /// </summary>
    internal const int MaxQuoteExampleLength = 300;

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

        // Unknown words are a 200 with "entries": [], so any non-2xx
        // (including 404) is a failure, never NotFound.
        if (status != HttpStatusCode.OK)
        {
            throw new DictionaryLookupException(
                $"{SourceId} returned HTTP {(int)status}.");
        }

        return Parse(body);
    }

    internal static DictionaryLookupResult Parse(string body)
    {
        FreeApiResponse? response;

        try
        {
            response = JsonSerializer.Deserialize(
                body,
                FreeDictionaryApiJsonContext.Default.FreeApiResponse);
        }
        catch (JsonException ex)
        {
            throw new DictionaryLookupException(
                $"{SourceId} returned an unexpected response.",
                ex);
        }

        // A missing "entries" array means this isn't the API's reply; it must
        // not be cached as NotFound.
        if (response?.Entries is not { } entries)
        {
            throw new DictionaryLookupException(
                $"{SourceId} returned an unexpected response.");
        }

        var builder = new MeaningListBuilder();

        foreach (var entry in entries)
        {
            AddSenses(builder, entry.PartOfSpeech, entry.Senses);
        }

        var meanings = builder.Build();
        if (meanings.Count == 0)
        {
            return DictionaryLookupResult.NotFound(body);
        }

        var ipa = entries
            .SelectMany(e => e.Pronunciations ?? [])
            .Where(p => string.Equals(p.Type, "ipa", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new DictionaryLookupResult
        {
            Status = DictionaryLookupStatus.Found,

            // Untagged first (the general pronunciation), then regional ones
            // ("US", "Received Pronunciation").
            Phonetic = MeaningListBuilder.FirstPhonetic(
                ipa
                    .OrderBy(p => p.Tags is { Count: > 0 } ? 1 : 0)
                    .Select(p => p.Text)
            ),
            Meanings = meanings,
            RawResponse = body,
        };
    }

    /// <summary>
    /// Subsenses (nested to any depth) become definitions straight after 
    /// their parent.
    /// </summary>
    private static void AddSenses(
        MeaningListBuilder builder,
        string? partOfSpeech,
        IReadOnlyList<FreeApiSense>? senses)
    {
        foreach (var sense in senses ?? [])
        {
            builder.Add(partOfSpeech, sense.Definition, Example(sense));

            AddSenses(builder, partOfSpeech, sense.Subsenses);
        }
    }

    private static string? Example(FreeApiSense sense) =>
        sense
            .Examples?
            .Select(e => e?.Trim()).FirstOrDefault(e => !string.IsNullOrEmpty(e))
                ?? sense.Quotes?
                    .Select(q => q.Text?.Trim())
                    .FirstOrDefault(q => !string.IsNullOrEmpty(q)
                        && q.Length <= MaxQuoteExampleLength);
}
