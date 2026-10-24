using Bookside.Core.Common;

namespace Bookside.Core.Vocabulary;

/// <summary>
/// Cached dictionary lookup, shared by every <see cref="VocabularyWord"/> with the same term.
/// Unique per (language, normalized term). NotFound results are cached too so unknown words
/// don't keep hitting the API.
/// </summary>
public class DictionaryEntry : Entity
{
    public required string NormalizedTerm { get; set; }

    public required string Language { get; set; }

    public DictionaryLookupStatus Status { get; set; }

    /// <summary>Provider id, e.g. "dictionaryapi.dev".</summary>
    public required string Source { get; set; }

    public DateTime FetchedAtUtc { get; set; }

    public string? Phonetic { get; set; }

    public string? AudioUrl { get; set; }

    /// <summary>Stored as a JSON column.</summary>
    public List<DictionaryMeaning> Meanings { get; set; } = [];

    /// <summary>Raw provider payload, kept so entries can be re-parsed without re-fetching.</summary>
    public string? RawResponse { get; set; }
}
