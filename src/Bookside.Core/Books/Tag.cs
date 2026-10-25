using Bookside.Core.Common;

namespace Bookside.Core.Books;

/// <summary>
/// User-defined label such as "Own" or "Have e-book". Kept when no book uses it.
/// </summary>
public class Tag : Entity
{
    public const int NameMaxLength = 50;

    public required string Name { get; set; }

    /// <summary>
    /// <see cref="Vocabulary.TermNormalizer"/> form of <see cref="Name"/>, 
    /// set on save; unique.
    /// </summary>
    public string NormalizedName { get; set; } = string.Empty;

    public ICollection<BookTag> Books { get; } = [];
}
