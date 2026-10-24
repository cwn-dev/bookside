using Bookside.Core.Common;
using Bookside.Core.Vocabulary;
using Bookside.Core.Vocabulary.Services;
using Microsoft.EntityFrameworkCore;

namespace Bookside.Data.Services;

internal sealed class VocabularyService(
    IDbContextFactory<BooksideDbContext> dbFactory,
    IDefinitionService definitions) : IVocabularyService
{
    public async Task<PagedResult<WordSummary>> ListWordsAsync(WordQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (skip, take) = Paging.Clamp(query.Skip, query.Take);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var words = db.VocabularyWords.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Contains(TermNormalizer.Normalize(query.Search));
            words = words.Where(w => EF.Functions.Like(w.NormalizedTerm, pattern, LikePattern.EscapeCharacter));
        }

        if (query.BookId is { } bookId)
        {
            words = words.Where(w => w.BookId == bookId);
        }

        var total = await words.CountAsync(cancellationToken).ConfigureAwait(false);

        words = query.Sort == WordSort.Alphabetical
            ? words.OrderBy(w => w.NormalizedTerm)
            : words.OrderByDescending(w => w.CreatedAtUtc).ThenByDescending(w => w.Id);

        var rows = await words
            .Skip(skip)
            .Take(take)
            .Include(w => w.Book)
            .Include(w => w.DictionaryEntry)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows.Select(w => new WordSummary
        {
            Id = w.Id,
            Term = w.Term,
            Language = w.Language,
            BookId = w.BookId,
            BookTitle = w.Book?.Title,
            DefinitionStatus = w.DictionaryEntry?.Status,
            ShortDefinition = w.DictionaryEntry?.Meanings.SelectMany(m => m.Definitions).FirstOrDefault()?.Text,
            CreatedAtUtc = w.CreatedAtUtc,
        }).ToList();

        return new PagedResult<WordSummary>(items, total);
    }

    public async Task<Result<WordDetails>> GetWordAsync(Guid wordId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await LoadDetailsAsync(db, wordId, cancellationToken).ConfigureAwait(false) is { } details
            ? details
            : WordNotFound(wordId);
    }

    public async Task<Result<WordDetails>> AddWordAsync(WordInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var word = new VocabularyWord { Term = string.Empty };
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (await ApplyInputAsync(db, input, word, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        db.VocabularyWords.Add(word);
        if (await SaveWordAsync(db, word, cancellationToken).ConfigureAwait(false) is { } conflict)
        {
            return conflict;
        }

        // Best effort: a dictionary outage must not lose the word.
        await LinkDefinitionAsync(db, word, refresh: false, cancellationToken).ConfigureAwait(false);
        return (await LoadDetailsAsync(db, word.Id, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<Result<WordDetails>> UpdateWordAsync(Guid wordId, WordInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var word = await db.VocabularyWords.SingleOrDefaultAsync(w => w.Id == wordId, cancellationToken).ConfigureAwait(false);
        if (word is null)
        {
            return WordNotFound(wordId);
        }

        var previousKey = (word.Language, word.NormalizedTerm);
        if (await ApplyInputAsync(db, input, word, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var termChanged = previousKey != (word.Language, word.NormalizedTerm);
        if (termChanged)
        {
            word.DictionaryEntryId = null;
        }

        if (await SaveWordAsync(db, word, cancellationToken).ConfigureAwait(false) is { } conflict)
        {
            return conflict;
        }

        if (termChanged)
        {
            await LinkDefinitionAsync(db, word, refresh: false, cancellationToken).ConfigureAwait(false);
        }

        return (await LoadDetailsAsync(db, word.Id, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<Result> DeleteWordAsync(Guid wordId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var deleted = await db.VocabularyWords.Where(w => w.Id == wordId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        return deleted == 0 ? WordNotFound(wordId) : Result.Success();
    }

    public async Task<Result<WordDetails>> DefineWordAsync(Guid wordId, bool refresh = false, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var word = await db.VocabularyWords.SingleOrDefaultAsync(w => w.Id == wordId, cancellationToken).ConfigureAwait(false);
        if (word is null)
        {
            return WordNotFound(wordId);
        }

        var linked = await LinkDefinitionAsync(db, word, refresh, cancellationToken).ConfigureAwait(false);
        if (!linked.IsSuccess)
        {
            return linked.Error;
        }

        return (await LoadDetailsAsync(db, word.Id, cancellationToken).ConfigureAwait(false))!;
    }

    private static ServiceError WordNotFound(Guid wordId) =>
        ServiceError.NotFound(ErrorCodes.WordNotFound, $"Word {wordId} was not found.");

    private static ServiceError DuplicateWord(string term) =>
        ServiceError.Conflict(ErrorCodes.DuplicateWord, $"\"{term}\" is already on your list.");

    /// <summary>Validates onto <paramref name="word"/> (including NormalizedTerm); returns the error, or null when valid.</summary>
    private static async Task<ServiceError?> ApplyInputAsync(
        BooksideDbContext db,
        WordInput input,
        VocabularyWord word,
        CancellationToken cancellationToken)
    {
        var v = new InputValidator();
        var term = v.Required(nameof(WordInput.Term), input.Term, VocabularyWord.TermMaxLength);
        var language = v.Required(nameof(WordInput.Language), input.Language, VocabularyWord.LanguageMaxLength);
        var context = v.Optional(nameof(WordInput.Context), input.Context, VocabularyWord.ContextMaxLength);
        var notes = v.Optional(nameof(WordInput.Notes), input.Notes, VocabularyWord.NotesMaxLength);
        v.Check(input.PageNumber is null or > 0, nameof(WordInput.PageNumber), "Must be greater than zero.");

        if (input.BookId is { } bookId && !await db.Books.AnyAsync(b => b.Id == bookId, cancellationToken).ConfigureAwait(false))
        {
            v.Add(nameof(WordInput.BookId), "Book not found.");
        }

        if (!v.IsValid)
        {
            return v.ToError();
        }

        var normalized = TermNormalizer.Normalize(term);
        var duplicate = await db.VocabularyWords
            .AnyAsync(w => w.Id != word.Id && w.Language == language && w.NormalizedTerm == normalized, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
        {
            return DuplicateWord(term);
        }

        word.Term = term;
        word.NormalizedTerm = normalized;
        word.Language = language;
        word.BookId = input.BookId;
        word.Context = context;
        word.PageNumber = input.PageNumber;
        word.Notes = notes;
        return null;
    }

    /// <summary>Saves, mapping a lost race on the unique (language, term) index to a conflict.</summary>
    private static async Task<ServiceError?> SaveWordAsync(BooksideDbContext db, VocabularyWord word, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (DbUpdateException)
        {
            var duplicate = await db.VocabularyWords.AsNoTracking()
                .AnyAsync(w => w.Id != word.Id && w.Language == word.Language && w.NormalizedTerm == word.NormalizedTerm, cancellationToken)
                .ConfigureAwait(false);
            if (!duplicate)
            {
                throw;
            }

            return DuplicateWord(word.Term);
        }
    }

    private async Task<Result> LinkDefinitionAsync(
        BooksideDbContext db,
        VocabularyWord word,
        bool refresh,
        CancellationToken cancellationToken)
    {
        var definition = await definitions.GetDefinitionAsync(word.Term, word.Language, refresh, cancellationToken).ConfigureAwait(false);
        if (!definition.IsSuccess)
        {
            return definition.Error;
        }

        if (word.DictionaryEntryId != definition.Value.Id)
        {
            word.DictionaryEntryId = definition.Value.Id;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    private static async Task<WordDetails?> LoadDetailsAsync(BooksideDbContext db, Guid wordId, CancellationToken cancellationToken)
    {
        var word = await db.VocabularyWords.AsNoTracking()
            .Include(w => w.Book)
            .Include(w => w.DictionaryEntry)
            .SingleOrDefaultAsync(w => w.Id == wordId, cancellationToken)
            .ConfigureAwait(false);

        return word is null
            ? null
            : new WordDetails
            {
                Id = word.Id,
                Term = word.Term,
                NormalizedTerm = word.NormalizedTerm,
                Language = word.Language,
                BookId = word.BookId,
                BookTitle = word.Book?.Title,
                Context = word.Context,
                PageNumber = word.PageNumber,
                Notes = word.Notes,
                Definition = word.DictionaryEntry is { } entry ? DefinitionService.ToDto(entry) : null,
                CreatedAtUtc = word.CreatedAtUtc,
                UpdatedAtUtc = word.UpdatedAtUtc,
            };
    }
}
