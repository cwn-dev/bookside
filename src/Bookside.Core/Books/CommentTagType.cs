namespace Bookside.Core.Books;

/// <summary>
/// Kind of an inline comment tag; see <see cref="CommentTagSyntax"/>.
/// </summary>
public enum CommentTagType
{
    /// <summary>
    /// A character or other person, typed as <c>@</c>; scoped to one book.
    /// </summary>
    Person = 0,

    /// <summary>
    /// Anything else, typed as <c>#</c>; shared across all books.
    /// </summary>
    General = 1,

    /// <summary>
    /// A page number, typed as <c>p.42</c> or <c>p. 42</c>; scoped to one book.
    /// </summary>
    Page = 2,
}
