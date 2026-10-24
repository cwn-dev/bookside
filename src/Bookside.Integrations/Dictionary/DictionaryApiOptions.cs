namespace Bookside.Integrations.Dictionary;

public sealed class DictionaryApiOptions
{
    /// <summary>Must end with a slash; request paths are relative to it.</summary>
    public Uri BaseAddress { get; set; } = new("https://api.dictionaryapi.dev/api/v2/");
}
