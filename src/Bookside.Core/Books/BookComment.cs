using Bookside.Core.Common;

namespace Bookside.Core.Books;

/// <summary>
/// A free-text note about the book as a whole, not tied to one read-through 
/// (that's <see cref="ReadingSession.Notes"/>).
/// Plain text; line breaks are kept. Edited when <see cref="Entity.UpdatedAtUtc"/>
/// is later than <see cref="Entity.CreatedAtUtc"/>.
/// </summary>
public class BookComment : Entity
{
    public const int TextMaxLength = 10_000;

    public Guid BookId { get; set; }

    public Book Book { get; set; } = null!;

    /// <summary>
    /// May hold inline tags (<see cref="CommentTagSyntax"/>), linked through
    /// <see cref="Tags"/>.
    /// </summary>
    public required string Text { get; set; }

    public ICollection<BookCommentTag> Tags { get; } = [];
}
