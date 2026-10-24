using Bookside.Core.Vocabulary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookside.Data.Configurations;

internal sealed class DictionaryEntryConfiguration : IEntityTypeConfiguration<DictionaryEntry>
{
    public void Configure(EntityTypeBuilder<DictionaryEntry> builder)
    {
        builder.ToTable("DictionaryEntries");
        builder.ConfigureEntity();

        builder.Property(e => e.NormalizedTerm).HasMaxLength(200);
        builder.Property(e => e.Language).HasMaxLength(16).IsUnicode(false);
        builder.Property(e => e.Source).HasMaxLength(50);
        builder.Property(e => e.Phonetic).HasMaxLength(200);
        builder.Property(e => e.AudioUrl).HasMaxLength(2000);

        builder.HasIndex(e => new { e.Language, e.NormalizedTerm }).IsUnique();

        // EF Core 10 complex collection mapped to a single JSON column.
        builder.ComplexCollection(e => e.Meanings, meaning =>
        {
            meaning.ToJson();
            meaning.ComplexCollection(m => m.Definitions);
        });
    }
}
