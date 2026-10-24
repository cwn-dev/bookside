using Bookside.Core.Vocabulary.Services;

namespace Bookside.UI.Forms;

/// <summary>Mutable form state for <see cref="WordInput"/>; property names match the input.</summary>
public sealed class WordFormModel
{
    public string? Term { get; set; }

    public string Language { get; set; } = "en";

    public Guid? BookId { get; set; }

    /// <summary>Display only: the title shown by the book picker for <see cref="BookId"/>.</summary>
    public string? BookTitle { get; set; }

    public string? Context { get; set; }

    public int? PageNumber { get; set; }

    public string? Notes { get; set; }

    public static WordFormModel From(WordDetails word)
    {
        ArgumentNullException.ThrowIfNull(word);
        return new WordFormModel
        {
            Term = word.Term,
            Language = word.Language,
            BookId = word.BookId,
            BookTitle = word.BookTitle,
            Context = word.Context,
            PageNumber = word.PageNumber,
            Notes = word.Notes,
        };
    }

    public WordInput ToInput() => new()
    {
        Term = Term ?? "",
        Language = Language,
        BookId = BookId,
        Context = Context,
        PageNumber = PageNumber,
        Notes = Notes,
    };
}
