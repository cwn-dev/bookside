using Bookside.Core.Books;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookside.Data.Configurations;

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.ConfigureEntity();

        builder.Property(t => t.Name).HasMaxLength(Tag.NameMaxLength);
        builder.Property(t => t.NormalizedName).HasMaxLength(Tag.NameMaxLength);
        builder.HasIndex(t => t.NormalizedName).IsUnique();
    }
}
