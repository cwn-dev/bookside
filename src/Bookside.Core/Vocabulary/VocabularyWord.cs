using Bookside.Core.Books;
using Bookside.Core.Common;

namespace Bookside.Core.Vocabulary;

/// <summary>A word on my list. Unique per (language, normalized term).</summary>
public class VocabularyWord : Entity
{
    /// <summary>As typed by the user.</summary>
    public required string Term { get; set; }

    /// <summary>See <see cref="TermNormalizer"/>. Kept in sync by the data layer.</summary>
    public string NormalizedTerm { get; set; } = string.Empty;

    /// <summary>BCP-47-ish language code; "en" for now.</summary>
    public string Language { get; set; } = "en";

    /// <summary>The book I found it in, if any.</summary>
    public Guid? BookId { get; set; }

    public Book? Book { get; set; }

    /// <summary>The sentence it appeared in.</summary>
    public string? Context { get; set; }

    public int? PageNumber { get; set; }

    public string? Notes { get; set; }

    /// <summary>Set once a definition has been looked up (from cache or API).</summary>
    public Guid? DictionaryEntryId { get; set; }

    public DictionaryEntry? DictionaryEntry { get; set; }
}
