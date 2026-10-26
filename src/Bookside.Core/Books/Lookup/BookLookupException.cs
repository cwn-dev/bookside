namespace Bookside.Core.Books.Lookup;

/// <summary>
/// Transient/provider failure; the next provider is tried.
/// </summary>
public class BookLookupException : Exception
{
    public BookLookupException()
    {
    }

    public BookLookupException(string message)
        : base(message)
    {
    }

    public BookLookupException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
