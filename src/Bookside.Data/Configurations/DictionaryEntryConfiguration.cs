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

        builder.Property(e => e.NormalizedTerm).HasMaxLength(VocabularyWord.TermMaxLength);
        builder.Property(e => e.Language).HasMaxLength(VocabularyWord.LanguageMaxLength).IsUnicode(false);
        builder.Property(e => e.Source).HasMaxLength(DictionaryEntry.SourceMaxLength);
        builder.Property(e => e.Phonetic).HasMaxLength(DictionaryEntry.PhoneticMaxLength);
        builder.Property(e => e.AudioUrl).HasMaxLength(DictionaryEntry.AudioUrlMaxLength);

        builder.HasIndex(e => new { e.Language, e.NormalizedTerm }).IsUnique();

        // EF Core 10 complex collection mapped to a single JSON column.
        builder.ComplexCollection(e => e.Meanings, meaning =>
        {
            meaning.ToJson();
            meaning.ComplexCollection(m => m.Definitions);
        });
    }
}
