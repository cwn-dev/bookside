using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bookside.Integrations.Dictionary;

// Wire format of GET /entries/{language}/{word}: an array of entries (one per etymology).
// Only the fields Bookside uses; everything else stays in RawResponse.

internal sealed record ApiEntry(
    string? Word,
    string? Phonetic,
    IReadOnlyList<ApiPhonetic>? Phonetics,
    IReadOnlyList<ApiMeaning>? Meanings);

internal sealed record ApiPhonetic(string? Text, string? Audio);

internal sealed record ApiMeaning(string? PartOfSpeech, IReadOnlyList<ApiDefinition>? Definitions);

internal sealed record ApiDefinition(string? Definition, string? Example);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(List<ApiEntry>))]
internal sealed partial class DictionaryApiJsonContext : JsonSerializerContext;
