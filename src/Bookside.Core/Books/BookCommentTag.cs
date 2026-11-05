namespace Bookside.Core.Books;

public class BookCommentTag
{
    public Guid CommentId { get; set; }

    public BookComment Comment { get; set; } = null!;

    public Guid TagId { get; set; }

    public CommentTag Tag { get; set; } = null!;
}
