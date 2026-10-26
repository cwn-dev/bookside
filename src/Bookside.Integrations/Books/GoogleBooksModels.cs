using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bookside.Integrations.Books;

// Wire format of GET googleapis.com/books/v1/volumes?q=… (only the fields
// Bookside uses). No matches: {"kind":"books#volumes","totalItems":0} without
// "items".

internal sealed record GbResponse(string? Kind, IReadOnlyList<GbVolume>? Items);

internal sealed record GbVolume(string? Id, GbVolumeInfo? VolumeInfo);

internal sealed record GbVolumeInfo(
    string? Title,
    string? Subtitle,
    IReadOnlyList<string>? Authors,
    string? Publisher,
    string? PublishedDate,
    string? Description,
    IReadOnlyList<GbIdentifier>? IndustryIdentifiers,
    int? PageCount,
    GbImageLinks? ImageLinks);

internal sealed record GbIdentifier(string? Type, string? Identifier);

internal sealed record GbImageLinks(string? Thumbnail);

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(GbResponse))]
internal sealed partial class GoogleBooksJsonContext : JsonSerializerContext;
