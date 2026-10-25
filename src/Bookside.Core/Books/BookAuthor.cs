namespace Bookside.Core.Books;

/// <summary>
/// Explicit join so author order (as printed on the cover) is preserved.
/// </summary>
public class BookAuthor
{
    public Guid BookId { get; set; }

    public Book Book { get; set; } = null!;

    public Guid AuthorId { get; set; }

    public Author Author { get; set; } = null!;

    /// <summary>
    /// Zero-based display order.
    /// </summary>
    public int Position { get; set; }
}
