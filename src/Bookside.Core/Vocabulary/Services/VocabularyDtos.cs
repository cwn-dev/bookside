namespace Bookside.Core.Vocabulary.Services;

public sealed record WordInput
{
    public required string Term { get; init; }

    public string Language { get; init; } = "en";

    public Guid? BookId { get; init; }

    public string? Context { get; init; }

    public int? PageNumber { get; init; }

    public string? Notes { get; init; }
}

public enum WordSort
{
    RecentlyAdded = 0,

    /// <summary>By normalized (lower-case) term, so effectively case-insensitive.</summary>
    Alphabetical = 1,
}

public sealed record WordQuery
{
    /// <summary>Substring match on the normalized term.</summary>
    public string? Search { get; init; }

    public Guid? BookId { get; init; }

    public WordSort Sort { get; init; } = WordSort.RecentlyAdded;

    public int Skip { get; init; }

    public int Take { get; init; } = Common.Paging.DefaultTake;
}

public sealed record WordSummary
{
    public required Guid Id { get; init; }

    public required string Term { get; init; }

    public required string Language { get; init; }

    public Guid? BookId { get; init; }

    public string? BookTitle { get; init; }

    /// <summary>Null when no lookup has succeeded yet (never tried, or the dictionary was unavailable).</summary>
    public DictionaryLookupStatus? DefinitionStatus { get; init; }

    /// <summary>First definition, for list views.</summary>
    public string? ShortDefinition { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}

public sealed record WordDetails
{
    public required Guid Id { get; init; }

    public required string Term { get; init; }

    public required string NormalizedTerm { get; init; }

    public required string Language { get; init; }

    public Guid? BookId { get; init; }

    public string? BookTitle { get; init; }

    public string? Context { get; init; }

    public int? PageNumber { get; init; }

    public string? Notes { get; init; }

    /// <summary>Null when no lookup has succeeded yet; call <see cref="IVocabularyService.DefineWordAsync"/> to retry.</summary>
    public DefinitionDto? Definition { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}

public sealed record DefinitionDto
{
    /// <summary>The cached <see cref="DictionaryEntry"/> id.</summary>
    public required Guid Id { get; init; }

    public required string NormalizedTerm { get; init; }

    public required string Language { get; init; }

    public required DictionaryLookupStatus Status { get; init; }

    public required string Source { get; init; }

    public DateTime FetchedAtUtc { get; init; }

    public string? Phonetic { get; init; }

    public string? AudioUrl { get; init; }

    public IReadOnlyList<DictionaryMeaning> Meanings { get; init; } = [];
}
