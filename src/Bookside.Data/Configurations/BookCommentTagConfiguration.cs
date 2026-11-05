using Bookside.Core.Books;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookside.Data.Configurations;

internal sealed class BookCommentTagConfiguration : IEntityTypeConfiguration<BookCommentTag>
{
    public void Configure(EntityTypeBuilder<BookCommentTag> builder)
    {
        builder.ToTable("BookCommentTags");
        builder.HasKey(ct => new { ct.CommentId, ct.TagId });

        builder.HasOne(ct => ct.Comment)
            .WithMany(c => c.Tags)
            .HasForeignKey(ct => ct.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ct => ct.Tag)
            .WithMany(t => t.Comments)
            .HasForeignKey(ct => ct.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ct => ct.TagId);
    }
}
