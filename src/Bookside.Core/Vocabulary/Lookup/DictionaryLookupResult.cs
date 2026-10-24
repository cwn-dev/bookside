namespace Bookside.Core.Vocabulary.Lookup;

public sealed record DictionaryLookupResult
{
    public required DictionaryLookupStatus Status { get; init; }

    public string? Phonetic { get; init; }

    public string? AudioUrl { get; init; }

    public IReadOnlyList<DictionaryMeaning> Meanings { get; init; } = [];

    public string? RawResponse { get; init; }

    public static DictionaryLookupResult NotFound(string? rawResponse = null) =>
        new() { Status = DictionaryLookupStatus.NotFound, RawResponse = rawResponse };
}
