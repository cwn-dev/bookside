using Bookside.Core.Books;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookside.Data.Configurations;

internal sealed class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.ToTable("Authors");
        builder.ConfigureEntity();

        builder.Property(a => a.Name).HasMaxLength(300);
        builder.HasIndex(a => a.Name);
    }
}
