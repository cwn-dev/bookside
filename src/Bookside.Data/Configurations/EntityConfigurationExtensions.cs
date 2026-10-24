using Bookside.Core.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookside.Data.Configurations;

internal static class EntityConfigurationExtensions
{
    /// <summary>Shared mapping for <see cref="Entity"/>: client-generated UUIDv7 key.</summary>
    public static void ConfigureEntity<T>(this EntityTypeBuilder<T> builder)
        where T : Entity
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
    }
}
