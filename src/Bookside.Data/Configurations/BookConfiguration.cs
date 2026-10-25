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
            t.HasCheckConstraint(
                "CK_Books_SeriesNumber",
                FormattableString.Invariant($"SeriesNumber IS NULL OR (SeriesNumber >= 0 AND SeriesNumber <= {Book.MaxSeriesNumber})"));
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

        // Series left without books are deleted by the service; SetNull just keeps the FK safe.
        builder.HasOne(b => b.Series)
            .WithMany(s => s.Books)
            .HasForeignKey(b => b.SeriesId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(b => b.Cover)
            .WithOne(c => c.Book)
            .HasForeignKey<BookCover>(c => c.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.ReadingSessions)
            .WithOne(s => s.Book)
            .HasForeignKey(s => s.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Comments)
            .WithOne(c => c.Book)
            .HasForeignKey(c => c.BookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
