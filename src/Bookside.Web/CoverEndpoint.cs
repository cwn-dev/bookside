using System.Runtime.InteropServices;
using Bookside.Core.Books.Services;
using Bookside.UI;
using Microsoft.Net.Http.Headers;

namespace Bookside.Web;

internal static class CoverEndpoint
{
    /// <summary>
    /// GET /covers/{bookId}?v={sha256}. The ETag is the image hash, so conditional requests get a 304.
    /// When <c>v</c> matches the current hash the response is cacheable forever (a new cover means a new URL);
    /// otherwise the browser must revalidate.
    /// </summary>
    public static IEndpointRouteBuilder MapCoverEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(CoverUrls.RoutePattern, GetCoverAsync).WithName("GetCover");
        return endpoints;
    }

    private static async Task<IResult> GetCoverAsync(
        Guid bookId,
        string? v,
        ILibraryService library,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await library.GetCoverAsync(bookId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return Results.NotFound();
        }

        var cover = result.Value;
        response.Headers.CacheControl = string.Equals(v, cover.Sha256, StringComparison.OrdinalIgnoreCase)
            ? "private, max-age=31536000, immutable"
            : "private, no-cache";
        response.Headers.XContentTypeOptions = "nosniff";

        var bytes = MemoryMarshal.TryGetArray(cover.Content, out var segment) && segment.Offset == 0 && segment.Count == segment.Array!.Length
            ? segment.Array
            : cover.Content.ToArray();

        // Results.File evaluates If-None-Match / If-Modified-Since and returns 304 itself.
        return Results.File(
            bytes,
            cover.ContentType,
            lastModified: new DateTimeOffset(cover.UpdatedAtUtc, TimeSpan.Zero),
            entityTag: new EntityTagHeaderValue($"\"{cover.Sha256}\""));
    }
}
