using Bookside.Core.Books;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookside.Data.Configurations;

internal sealed class BookCommentConfiguration : IEntityTypeConfiguration<BookComment>
{
    public void Configure(EntityTypeBuilder<BookComment> builder)
    {
        builder.ToTable("BookComments");
        builder.ConfigureEntity();

        builder.Property(c => c.Text).HasMaxLength(BookComment.TextMaxLength);

        builder.HasIndex(c => new { c.BookId, c.CreatedAtUtc });
    }
}
