using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bookside.Integrations.Books;

// Wire formats of openlibrary.org (only the fields Bookside uses):
// GET /api/books?bibkeys=ISBN:{isbn}&jscmd=data&format=json
//     → {"ISBN:{isbn}": edition}, or {} when unknown.
// GET /search.json?q=… → {"docs": [work, …]}.

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

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(Dictionary<string, OlEdition>))]
[JsonSerializable(typeof(OlSearchResponse))]
internal sealed partial class OpenLibraryJsonContext : JsonSerializerContext;
