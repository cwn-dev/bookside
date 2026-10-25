using Bookside.Core.Common;

namespace Bookside.Core.Books;

public class Author : Entity
{
    public const int NameMaxLength = 300;

    public required string Name { get; set; }

    public ICollection<BookAuthor> Books { get; } = [];
}
