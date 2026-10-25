namespace Bookside.Core.Vocabulary;

/// <summary>
/// Definitions grouped by part of speech. Value object,
/// persisted inside <see cref="DictionaryEntry"/>.
/// </summary>
public class DictionaryMeaning
{
    public required string PartOfSpeech { get; set; }

    public List<DictionaryDefinition> Definitions { get; set; } = [];
}

public class DictionaryDefinition
{
    public required string Text { get; set; }

    public string? Example { get; set; }
}
