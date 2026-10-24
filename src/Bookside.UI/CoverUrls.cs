namespace Bookside.UI;

/// <summary>
/// The cover image route, shared by the components (which link to it) and the host (which maps it).
/// The URL carries the image hash, so a new cover gets a new URL and the old one can be cached forever.
/// </summary>
public static class CoverUrls
{
    public const string RoutePattern = "/covers/{bookId:guid}";

    /// <summary>Relative to the page's base href.</summary>
    public static string For(Guid bookId, string sha256) => $"covers/{bookId}?v={Uri.EscapeDataString(sha256)}";
}
