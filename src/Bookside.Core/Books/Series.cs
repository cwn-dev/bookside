using Bookside.Core.Common;

namespace Bookside.Core.Books;

public class Series : Entity
{
    public const int NameMaxLength = 300;

    public required string Name { get; set; }

    public ICollection<Book> Books { get; } = [];
}
