namespace Bookside.Core.Vocabulary.Lookup;

/// <summary>
/// Transient/provider failure; the result must not be cached.
/// </summary>
public class DictionaryLookupException : Exception
{
    public DictionaryLookupException()
    {
    }

    public DictionaryLookupException(string message)
        : base(message)
    {
    }

    public DictionaryLookupException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
