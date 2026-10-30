using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bookside.Integrations.Books;

// Wire format of googleapis.com/books/v1 (only the fields Bookside uses):
// GET volumes?q=… → {"kind":"books#volumes","items":[volume, …]}; no matches
// has no "items". GET volumes/{id} → volume, whose description is HTML (the
// search's is plain text with the paragraph breaks removed).

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
[JsonSerializable(typeof(GbVolume))]
internal sealed partial class GoogleBooksJsonContext : JsonSerializerContext;
