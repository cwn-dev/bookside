namespace Bookside.Integrations.Dictionary;

public sealed class FreeDictionaryApiOptions
{
    /// <summary>Must end with a slash; request paths are relative to it.</summary>
    public Uri BaseAddress { get; set; } = new("https://freedictionaryapi.com/api/v1/");
}
