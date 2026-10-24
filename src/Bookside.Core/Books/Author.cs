using Bookside.Core.Common;

namespace Bookside.Core.Books;

public class Author : Entity
{
    public required string Name { get; set; }

    public ICollection<BookAuthor> Books { get; } = new List<BookAuthor>();
}
