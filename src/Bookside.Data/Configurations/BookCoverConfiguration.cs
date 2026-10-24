using Bookside.Core.Books;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookside.Data.Configurations;

internal sealed class BookCoverConfiguration : IEntityTypeConfiguration<BookCover>
{
    public void Configure(EntityTypeBuilder<BookCover> builder)
    {
        builder.ToTable("BookCovers");
        builder.HasKey(c => c.BookId);

        builder.Property(c => c.Content).HasMaxLength(BookCover.MaxSizeBytes);
        builder.Property(c => c.ContentType).HasMaxLength(100).IsUnicode(false);
        builder.Property(c => c.Sha256).HasMaxLength(64).IsUnicode(false).IsFixedLength();
        builder.Property(c => c.SourceUrl).HasMaxLength(BookCover.SourceUrlMaxLength);
    }
}
