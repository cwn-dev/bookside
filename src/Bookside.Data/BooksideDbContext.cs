using Bookside.Core.Books;
using Bookside.Core.Vocabulary;
using Bookside.Data.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bookside.Data;

/// <summary>
/// Provider-agnostic model. The provider (and the migrations assembly) is chosen by the host via a
/// provider package such as Bookside.Data.Sqlite.
/// </summary>
public class BooksideDbContext(DbContextOptions<BooksideDbContext> options, TimeProvider? timeProvider = null)
    : DbContext(options)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public DbSet<Book> Books => Set<Book>();

    public DbSet<Author> Authors => Set<Author>();

    public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();

    public DbSet<BookCover> BookCovers => Set<BookCover>();

    public DbSet<ReadingSession> ReadingSessions => Set<ReadingSession>();

    public DbSet<BookComment> BookComments => Set<BookComment>();

    public DbSet<VocabularyWord> VocabularyWords => Set<VocabularyWord>();

    public DbSet<DictionaryEntry> DictionaryEntries => Set<DictionaryEntry>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplySaveRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplySaveRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BooksideDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // All DateTimes are UTC. Some providers (SQLite) lose DateTimeKind on round-trip; restore it on read.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

        // Enums as strings: readable in the DB and safe against reordering.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }

    private void ApplySaveRules()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            switch (entry.Entity)
            {
                case VocabularyWord word:
                    word.NormalizedTerm = TermNormalizer.Normalize(word.Term);
                    break;
                case DictionaryEntry dictionaryEntry:
                    dictionaryEntry.NormalizedTerm = TermNormalizer.Normalize(dictionaryEntry.NormalizedTerm);
                    break;
                case BookCover cover when cover.Content.Length > BookCover.MaxSizeBytes:
                    throw new InvalidOperationException(
                        $"Cover image is {cover.Content.Length:N0} bytes; the limit is {BookCover.MaxSizeBytes:N0}.");
            }

            Stamp(entry, now);
        }
    }

    private static void Stamp(EntityEntry entry, DateTime now)
    {
        if (entry.State == EntityState.Added && entry.Metadata.FindProperty("CreatedAtUtc") is not null)
        {
            entry.Property("CreatedAtUtc").CurrentValue = now;
        }

        if (entry.Metadata.FindProperty("UpdatedAtUtc") is not null)
        {
            entry.Property("UpdatedAtUtc").CurrentValue = now;
        }
    }
}
