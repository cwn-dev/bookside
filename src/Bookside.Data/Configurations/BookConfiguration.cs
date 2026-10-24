using Bookside.Core.Books;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookside.Data.Configurations;

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Books", t =>
        {
            t.HasCheckConstraint("CK_Books_PageCount", "PageCount IS NULL OR PageCount > 0");
        });
        builder.ConfigureEntity();

        builder.Property(b => b.Title).HasMaxLength(Book.TitleMaxLength);
        builder.Property(b => b.Subtitle).HasMaxLength(Book.SubtitleMaxLength);
        builder.Property(b => b.Isbn13).HasMaxLength(13).IsUnicode(false);
        builder.Property(b => b.Isbn10).HasMaxLength(10).IsUnicode(false);
        builder.Property(b => b.Publisher).HasMaxLength(Book.PublisherMaxLength);
        builder.Property(b => b.Description).HasMaxLength(Book.DescriptionMaxLength);
        builder.Property(b => b.MetadataSource).HasMaxLength(Book.MetadataSourceMaxLength);
        builder.Property(b => b.MetadataSourceId).HasMaxLength(Book.MetadataSourceIdMaxLength);

        builder.HasIndex(b => b.Title);
        // Not unique: owning two editions/copies with the same ISBN is legitimate.
        builder.HasIndex(b => b.Isbn13);

        builder.HasOne(b => b.Cover)
            .WithOne(c => c.Book)
            .HasForeignKey<BookCover>(c => c.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.ReadingSessions)
            .WithOne(s => s.Book)
            .HasForeignKey(s => s.BookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
