namespace Bookside.Integrations.Books;

public sealed class OpenLibraryOptions
{
    /// <summary>
    /// Must end with a slash; request paths are relative to it.
    /// </summary>
    public Uri BaseAddress { get; set; } = new("https://openlibrary.org/");
}
