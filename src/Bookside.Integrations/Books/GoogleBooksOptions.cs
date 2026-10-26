namespace Bookside.Integrations.Books;

public sealed class GoogleBooksOptions
{
    /// <summary>
    /// Must end with a slash; request paths are relative to it.
    /// </summary>
    public Uri BaseAddress { get; set; } = new("https://www.googleapis.com/books/v1/");

    /// <summary>
    /// Sent as the <c>X-Goog-Api-Key</c> header (not in the URL, so it stays
    /// out of request logs).
    /// </summary>
    public string? ApiKey { get; set; }
}
