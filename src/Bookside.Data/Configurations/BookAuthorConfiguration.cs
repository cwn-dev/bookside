using Bookside.Core.Books;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookside.Data.Configurations;

internal sealed class BookAuthorConfiguration : IEntityTypeConfiguration<BookAuthor>
{
    public void Configure(EntityTypeBuilder<BookAuthor> builder)
    {
        builder.ToTable("BookAuthors");
        builder.HasKey(ba => new { ba.BookId, ba.AuthorId });

        builder.HasOne(ba => ba.Book)
            .WithMany(b => b.Authors)
            .HasForeignKey(ba => ba.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting an author that still has books is a mistake; make the caller detach first.
        builder.HasOne(ba => ba.Author)
            .WithMany(a => a.Books)
            .HasForeignKey(ba => ba.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ba => ba.AuthorId);
    }
}
