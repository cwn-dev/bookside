using Bookside.Core.Common;

namespace Bookside.Core.Books;

/// <summary>
/// An inline tag used in book comments (see <see cref="CommentTagSyntax"/>).
/// Person and page tags belong to one book; general tags have no book and are
/// shared. Deleted when no comment uses it any more.
/// </summary>
public class CommentTag : Entity
{
    public const int NameMaxLength = 100;

    public CommentTagType Type { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// <see cref="Vocabulary.TermNormalizer"/> form of <see cref="Name"/>,
    /// set on save; unique per book and type, or overall (general).
    /// </summary>
    public string NormalizedName { get; set; } = string.Empty;

    /// <summary>
    /// Null for <see cref="CommentTagType.General"/> tags, set for the others.
    /// </summary>
    public Guid? BookId { get; set; }

    public Book? Book { get; set; }

    public ICollection<BookCommentTag> Comments { get; } = [];
}
