using Bookside.Core.Vocabulary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookside.Data.Configurations;

internal sealed class VocabularyWordConfiguration : IEntityTypeConfiguration<VocabularyWord>
{
    public void Configure(EntityTypeBuilder<VocabularyWord> builder)
    {
        builder.ToTable("VocabularyWords");
        builder.ConfigureEntity();

        builder.Property(w => w.Term).HasMaxLength(200);
        builder.Property(w => w.NormalizedTerm).HasMaxLength(200);
        builder.Property(w => w.Language).HasMaxLength(16).IsUnicode(false);
        builder.Property(w => w.Context).HasMaxLength(2000);
        builder.Property(w => w.Notes).HasMaxLength(4000);

        builder.HasIndex(w => new { w.Language, w.NormalizedTerm }).IsUnique();
        builder.HasIndex(w => w.BookId);

        // Deleting a book keeps the words; they just lose the "found in" link.
        builder.HasOne(w => w.Book)
            .WithMany()
            .HasForeignKey(w => w.BookId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(w => w.DictionaryEntry)
            .WithMany()
            .HasForeignKey(w => w.DictionaryEntryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
