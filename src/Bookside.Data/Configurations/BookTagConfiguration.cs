using Bookside.Core.Books;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookside.Data.Configurations;

internal sealed class BookTagConfiguration : IEntityTypeConfiguration<BookTag>
{
    public void Configure(EntityTypeBuilder<BookTag> builder)
    {
        builder.ToTable("BookTags");
        builder.HasKey(bt => new { bt.BookId, bt.TagId });

        builder.HasOne(bt => bt.Book)
            .WithMany(b => b.Tags)
            .HasForeignKey(bt => bt.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a tag removes it from its books.
        builder.HasOne(bt => bt.Tag)
            .WithMany(t => t.Books)
            .HasForeignKey(bt => bt.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(bt => bt.TagId);
    }
}
