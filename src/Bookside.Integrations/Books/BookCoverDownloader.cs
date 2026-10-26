using System.Net;
using Bookside.Core.Books;
using Bookside.Core.Books.Lookup;
using Bookside.Core.Books.Services;

namespace Bookside.Integrations.Books;

/// <summary>
/// Cover downloads for all book providers. Own client
/// (<see cref="ClientName"/>) with a longer budget than lookups: Open Library
/// covers redirect through archive.org and take 4–8 s.
/// </summary>
internal sealed class BookCoverDownloader(HttpClient http)
{
    public const string ClientName = IntegrationsServiceCollectionExtensions.BookCoverClientName;

    /// <summary>
    /// Only https URLs on <paramref name="hosts"/> (the provider's own image
    /// hosts), so a URL can't make the server fetch arbitrary addresses. Size
    /// is capped by the client's response buffer limit.
    /// </summary>
    public async Task<CoverInput> DownloadAsync(
        Uri url,
        IReadOnlySet<string> hosts,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(hosts);

        if (!url.IsAbsoluteUri
            || url.Scheme != Uri.UriSchemeHttps
            || !hosts.Contains(url.Host))
        {
            throw new BookLookupException($"{source} does not serve covers from {url}.");
        }

        var (status, body, contentType) = await LookupHttp
            .GetBytesAsync(
                http,
                url,
                ex => BookHttp.Unreachable(source, ex),
                cancellationToken)
            .ConfigureAwait(false);

        if (status != HttpStatusCode.OK
            || contentType is null
            || !BookCover.AllowedContentTypes.Contains(contentType)
            || body.Length == 0)
        {
            throw new BookLookupException(
                $"{source} returned no cover image "
                + $"(HTTP {(int)status}, {contentType ?? "no content type"}).");
        }

        return new CoverInput
        {
            Content = body,
            ContentType = contentType,
            SourceUrl = url.AbsoluteUri,
        };
    }
}
