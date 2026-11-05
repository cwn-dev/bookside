using Bookside.Core.Books;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookside.Data.Configurations;

internal sealed class CommentTagConfiguration : IEntityTypeConfiguration<CommentTag>
{
    public void Configure(EntityTypeBuilder<CommentTag> builder)
    {
        builder.ToTable("CommentTags", t => t.HasCheckConstraint(
            "CK_CommentTags_BookId",
            $"(Type = '{nameof(CommentTagType.General)}') = (BookId IS NULL)"));
        builder.ConfigureEntity();

        builder.Property(t => t.Name).HasMaxLength(CommentTag.NameMaxLength);
        builder.Property(t => t.NormalizedName).HasMaxLength(CommentTag.NameMaxLength);

        // Person and page tags go with their book.
        builder.HasOne(t => t.Book)
            .WithMany()
            .HasForeignKey(t => t.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        // SQLite treats NULLs as distinct, so general tags need their own index.
        builder.HasIndex(t => new { t.BookId, t.Type, t.NormalizedName })
            .IsUnique()
            .HasFilter("BookId IS NOT NULL");
        builder.HasIndex(t => t.NormalizedName)
            .IsUnique()
            .HasFilter("BookId IS NULL");
    }
}
