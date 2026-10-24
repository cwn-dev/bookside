using Bookside.Core.Common;
using Bookside.Core.Vocabulary;
using Bookside.Core.Vocabulary.Lookup;
using Bookside.Core.Vocabulary.Services;
using Microsoft.EntityFrameworkCore;

namespace Bookside.Data.Services;

internal sealed class DefinitionService(
    IDbContextFactory<BooksideDbContext> dbFactory,
    IEnumerable<IDictionaryLookupService> lookups,
    TimeProvider timeProvider) : IDefinitionService
{
    private readonly IDictionaryLookupService[] _providers = lookups.ToArray() is { Length: > 0 } providers
        ? providers
        : throw new InvalidOperationException("No IDictionaryLookupService is registered.");

    public async Task<Result<DefinitionDto>> GetDefinitionAsync(
        string term,
        string language = "en",
        bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        var v = new InputValidator();
        var normalized = TermNormalizer.Normalize(v.Required(nameof(term), term, VocabularyWord.TermMaxLength));
        var lang = v.Required(nameof(language), language, VocabularyWord.LanguageMaxLength);
        if (!v.IsValid)
        {
            return v.ToError();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entry = await FindAsync(db, lang, normalized, cancellationToken).ConfigureAwait(false);
        if (entry is not null && !refresh)
        {
            return ToDto(entry);
        }

        var (result, source, failure) = await LookupAsync(normalized, lang, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            // Transient: never cached. A previously cached entry (on refresh) is left untouched.
            return ServiceError.Unavailable(ErrorCodes.DictionaryUnavailable, $"The dictionary is unavailable: {failure}");
        }

        var isNew = entry is null;
        entry ??= db.DictionaryEntries.Add(new DictionaryEntry
        {
            NormalizedTerm = normalized,
            Language = lang,
            Source = source,
        }).Entity;

        entry.Source = source;
        entry.Status = result.Status;
        entry.FetchedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        entry.Phonetic = result.Phonetic;
        entry.AudioUrl = result.AudioUrl;
        entry.Meanings = result.Meanings.ToList();
        entry.RawResponse = result.RawResponse;

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException) when (isNew)
        {
            // A concurrent lookup cached the same term first (unique index); use that entry.
            db.ChangeTracker.Clear();
            var winner = await FindAsync(db, lang, normalized, cancellationToken).ConfigureAwait(false);
            if (winner is null)
            {
                throw;
            }

            return ToDto(winner);
        }

        return ToDto(entry);
    }

    /// <summary>
    /// Tries each provider in order until one finds the term. NotFound is only an outcome (and so cached) when
    /// every provider answered NotFound; the first one's reply is kept. If any provider failed and none found the
    /// term, the outcome is null (Unavailable), so a later retry can still reach the provider that was down.
    /// </summary>
    private async Task<(DictionaryLookupResult? Result, string Source, string? Failure)> LookupAsync(
        string term,
        string language,
        CancellationToken cancellationToken)
    {
        (DictionaryLookupResult Result, string Source)? notFound = null;
        List<string>? failures = null;
        foreach (var provider in _providers)
        {
            DictionaryLookupResult result;
            try
            {
                result = await provider.LookupAsync(term, language, cancellationToken).ConfigureAwait(false);
            }
            catch (DictionaryLookupException ex)
            {
                (failures ??= []).Add(ex.Message);
                continue;
            }

            if (result.Status == DictionaryLookupStatus.Found)
            {
                return (result, provider.Source, null);
            }

            notFound ??= (result, provider.Source);
        }

        return failures is null && notFound is { } nf
            ? (nf.Result, nf.Source, null)
            : (null, string.Empty, string.Join(" ", failures ?? []));
    }

    internal static DefinitionDto ToDto(DictionaryEntry entry) => new()
    {
        Id = entry.Id,
        NormalizedTerm = entry.NormalizedTerm,
        Language = entry.Language,
        Status = entry.Status,
        Source = entry.Source,
        FetchedAtUtc = entry.FetchedAtUtc,
        Phonetic = entry.Phonetic,
        AudioUrl = entry.AudioUrl,
        Meanings = entry.Meanings,
    };

    private static Task<DictionaryEntry?> FindAsync(BooksideDbContext db, string language, string normalized, CancellationToken cancellationToken) =>
        db.DictionaryEntries.SingleOrDefaultAsync(e => e.Language == language && e.NormalizedTerm == normalized, cancellationToken);
}
