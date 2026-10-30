using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bookside.Integrations.Books;

// Wire formats of openlibrary.org (only the fields Bookside uses):
// GET /api/books?bibkeys=ISBN:{isbn}&jscmd=data&format=json
//     → {"ISBN:{isbn}": edition}, or {} when unknown.
// GET /search.json?q=… → {"docs": [work, …]}.
// GET /books/OL…M.json, /works/OL…W.json → record; an edition lists its works.

internal sealed record OlEdition(
    string? Key,
    string? Title,
    string? Subtitle,
    IReadOnlyList<OlName>? Authors,
    int? NumberOfPages,
    OlIdentifiers? Identifiers,
    IReadOnlyList<OlName>? Publishers,
    string? PublishDate,
    OlCover? Cover);

internal sealed record OlName(string? Name);

internal sealed record OlIdentifiers(
    [property: JsonPropertyName("isbn_13")] IReadOnlyList<string>? Isbn13,
    [property: JsonPropertyName("isbn_10")] IReadOnlyList<string>? Isbn10);

internal sealed record OlCover(string? Large);

internal sealed record OlSearchResponse(IReadOnlyList<OlWork>? Docs);

internal sealed record OlWork(
    string? Key,
    string? Title,
    string? Subtitle,
    IReadOnlyList<string>? AuthorName,
    int? FirstPublishYear,
    long? CoverI,
    int? NumberOfPagesMedian);

internal sealed record OlRecord(JsonElement? Description, IReadOnlyList<OlKey>? Works);

internal sealed record OlKey(string? Key);

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(Dictionary<string, OlEdition>))]
[JsonSerializable(typeof(OlSearchResponse))]
[JsonSerializable(typeof(OlRecord))]
internal sealed partial class OpenLibraryJsonContext : JsonSerializerContext;
