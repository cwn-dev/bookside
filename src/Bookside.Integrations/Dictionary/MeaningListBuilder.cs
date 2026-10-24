using Bookside.Core.Vocabulary;

namespace Bookside.Integrations.Dictionary;

/// <summary>
/// Shared mapping rules for every provider: definitions are grouped by part of speech (case-insensitive,
/// first-seen order), blank and duplicate definitions are dropped, and a part of speech with no definitions
/// is left out.
/// </summary>
internal sealed class MeaningListBuilder
{
    private readonly List<DictionaryMeaning> _meanings = [];

    public void Add(string? partOfSpeech, string? definition, string? example)
    {
        var text = definition?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        partOfSpeech = partOfSpeech?.Trim();
        if (string.IsNullOrEmpty(partOfSpeech))
        {
            partOfSpeech = "unknown";
        }

        var meaning = _meanings.Find(m => string.Equals(m.PartOfSpeech, partOfSpeech, StringComparison.OrdinalIgnoreCase));
        if (meaning is null)
        {
            meaning = new DictionaryMeaning { PartOfSpeech = partOfSpeech };
            _meanings.Add(meaning);
        }
        else if (meaning.Definitions.Exists(d => d.Text == text))
        {
            return;
        }

        example = example?.Trim();
        meaning.Definitions.Add(new DictionaryDefinition { Text = text, Example = string.IsNullOrEmpty(example) ? null : example });
    }

    public List<DictionaryMeaning> Build() => _meanings;

    /// <summary>First non-blank value that fits <see cref="DictionaryEntry.PhoneticMaxLength"/>.</summary>
    public static string? FirstPhonetic(IEnumerable<string?> candidates) =>
        candidates
            .Select(p => p?.Trim())
            .FirstOrDefault(p => !string.IsNullOrEmpty(p) && p.Length <= DictionaryEntry.PhoneticMaxLength);
}
