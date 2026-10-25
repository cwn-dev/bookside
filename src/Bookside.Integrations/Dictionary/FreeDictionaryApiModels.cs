using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bookside.Integrations.Dictionary;

// Wire format of freedictionaryapi.com GET /api/v1/entries/{language}/{word}:
// one object with one entry per part of speech (and etymology). Only the fields 
// Bookside uses; everything else stays in RawResponse.

internal sealed record FreeApiResponse(
    string? Word,
    IReadOnlyList<FreeApiEntry>? Entries);

internal sealed record FreeApiEntry(
    string? PartOfSpeech,
    IReadOnlyList<FreeApiPronunciation>? Pronunciations,
    IReadOnlyList<FreeApiSense>? Senses);

internal sealed record FreeApiPronunciation(
    string? Type,
    string? Text,
    IReadOnlyList<string>? Tags);

internal sealed record FreeApiSense(
    string? Definition,
    IReadOnlyList<string>? Examples,
    IReadOnlyList<FreeApiQuote>? Quotes,
    IReadOnlyList<FreeApiSense>? Subsenses);

internal sealed record FreeApiQuote(
    string? Text,
    string? Reference);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(FreeApiResponse))]
internal sealed partial class FreeDictionaryApiJsonContext : JsonSerializerContext;
