using Bookside.Core.Common;
using Bookside.Core.Vocabulary;
using Bookside.Core.Vocabulary.Services;
using Microsoft.EntityFrameworkCore;

namespace Bookside.Data.Tests.Services;

public sealed class VocabularyServiceTests : ServiceTestBase
{
    [Fact]
    public async Task Adding_a_word_attaches_a_definition_from_the_dictionary()
    {
        Dictionary.Define("sesquipedalian", "Given to using long words.");
        var book = await AddBookAsync("Mort");

        var word = Success(await Vocabulary.AddWordAsync(
            new WordInput { Term = " Sesquipedalian ", BookId = book.Id, Context = "A sesquipedalian sentence.", PageNumber = 12 },
            Ct));

        Assert.Equal("Sesquipedalian", word.Term);
        Assert.Equal("sesquipedalian", word.NormalizedTerm);
        Assert.Equal("Mort", word.BookTitle);
        Assert.NotNull(word.Definition);
        Assert.Equal(DictionaryLookupStatus.Found, word.Definition.Status);
        Assert.Equal("Given to using long words.", word.Definition.Meanings[0].Definitions[0].Text);
        Assert.Equal(["sesquipedalian"], Dictionary.Calls);
    }

    [Fact]
    public async Task Duplicate_words_are_a_conflict_after_normalization()
    {
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "Ennui" }, Ct));

        var conflict = Failure(await Vocabulary.AddWordAsync(new WordInput { Term = "  ENNUI " }, Ct), ErrorType.Conflict);

        Assert.Equal(ErrorCodes.DuplicateWord, conflict.Code);
    }

    [Fact]
    public async Task Invalid_word_input_returns_field_errors()
    {
        var error = Failure(
            await Vocabulary.AddWordAsync(new WordInput { Term = " ", PageNumber = 0, BookId = Guid.CreateVersion7() }, Ct),
            ErrorType.Validation);

        Assert.Equal(["BookId", "PageNumber", "Term"], error.FieldErrors.Select(e => e.Field).Order());
    }

    [Fact]
    public async Task Dictionary_outage_still_saves_the_word_and_define_retries_later()
    {
        Dictionary.Unavailable = true;
        var word = Success(await Vocabulary.AddWordAsync(new WordInput { Term = "petrichor" }, Ct));
        Assert.Null(word.Definition);

        var stillDown = Failure(await Vocabulary.DefineWordAsync(word.Id, cancellationToken: Ct), ErrorType.Unavailable);
        Assert.Equal(ErrorCodes.DictionaryUnavailable, stillDown.Code);

        await using (var db = Database.CreateContext())
        {
            Assert.Empty(await db.DictionaryEntries.ToListAsync(Ct)); // transient failures are never cached
        }

        Dictionary.Unavailable = false;
        Dictionary.Define("petrichor", "The smell of rain on dry earth.");
        var defined = Success(await Vocabulary.DefineWordAsync(word.Id, cancellationToken: Ct));
        Assert.Equal(DictionaryLookupStatus.Found, defined.Definition?.Status);
    }

    [Fact]
    public async Task Cached_definitions_are_shared_and_not_refetched()
    {
        Dictionary.Define("ennui", "Listlessness.");

        var first = Success(await Definitions.GetDefinitionAsync("Ennui", cancellationToken: Ct));
        var second = Success(await Definitions.GetDefinitionAsync("  ennui", cancellationToken: Ct));
        var word = Success(await Vocabulary.AddWordAsync(new WordInput { Term = "ENNUI" }, Ct));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Id, word.Definition?.Id);
        Assert.Equal(["ennui"], Dictionary.Calls);
    }

    [Fact]
    public async Task Not_found_is_cached_until_an_explicit_refresh()
    {
        var word = Success(await Vocabulary.AddWordAsync(new WordInput { Term = "skibidi" }, Ct));
        Assert.Equal(DictionaryLookupStatus.NotFound, word.Definition?.Status);

        Success(await Vocabulary.DefineWordAsync(word.Id, cancellationToken: Ct));
        Assert.Single(Dictionary.Calls);

        Dictionary.Define("skibidi", "A new word.");
        var refreshed = Success(await Vocabulary.DefineWordAsync(word.Id, refresh: true, Ct));

        Assert.Equal(2, Dictionary.Calls.Count);
        Assert.Equal(DictionaryLookupStatus.Found, refreshed.Definition?.Status);
        Assert.Equal(word.Definition?.Id, refreshed.Definition?.Id); // same cache row, updated in place
    }

    [Fact]
    public async Task Failed_refresh_keeps_the_cached_entry()
    {
        Dictionary.Define("ennui", "Listlessness.");
        Success(await Definitions.GetDefinitionAsync("ennui", cancellationToken: Ct));

        Dictionary.Unavailable = true;
        Failure(await Definitions.GetDefinitionAsync("ennui", refresh: true, cancellationToken: Ct), ErrorType.Unavailable);

        Dictionary.Unavailable = false;
        Dictionary.Entries.Clear();
        var cached = Success(await Definitions.GetDefinitionAsync("ennui", cancellationToken: Ct));
        Assert.Equal(DictionaryLookupStatus.Found, cached.Status);
    }

    [Fact]
    public async Task Changing_the_term_relinks_the_definition()
    {
        Dictionary.Define("ennui", "Listlessness.");
        Dictionary.Define("anomie", "Social instability.");
        var word = Success(await Vocabulary.AddWordAsync(new WordInput { Term = "ennui", Notes = "first" }, Ct));

        var notesOnly = Success(await Vocabulary.UpdateWordAsync(word.Id, new WordInput { Term = "Ennui", Notes = "second" }, Ct));
        Assert.Equal(word.Definition?.Id, notesOnly.Definition?.Id);
        Assert.Single(Dictionary.Calls);

        var renamed = Success(await Vocabulary.UpdateWordAsync(word.Id, new WordInput { Term = "anomie" }, Ct));
        Assert.Equal("Social instability.", renamed.Definition?.Meanings[0].Definitions[0].Text);
        Assert.Null(renamed.Notes);
    }

    [Fact]
    public async Task Renaming_onto_an_existing_word_is_a_conflict()
    {
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "ennui" }, Ct));
        var other = Success(await Vocabulary.AddWordAsync(new WordInput { Term = "anomie" }, Ct));

        Failure(await Vocabulary.UpdateWordAsync(other.Id, new WordInput { Term = "Ennui" }, Ct), ErrorType.Conflict);
    }

    [Fact]
    public async Task List_searches_filters_by_book_and_shows_a_short_definition()
    {
        Dictionary.Define("ennui", "Listlessness.");
        var book = await AddBookAsync("Mort");
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "Ennui", BookId = book.Id }, Ct));
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "anomie" }, Ct));
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = "Zeitgeist", BookId = book.Id }, Ct));

        var alphabetical = await Vocabulary.ListWordsAsync(new WordQuery { Sort = WordSort.Alphabetical }, Ct);
        Assert.Equal(["anomie", "Ennui", "Zeitgeist"], alphabetical.Items.Select(w => w.Term));

        var fromBook = await Vocabulary.ListWordsAsync(new WordQuery { BookId = book.Id }, Ct);
        Assert.Equal(["Zeitgeist", "Ennui"], fromBook.Items.Select(w => w.Term)); // newest first
        Assert.All(fromBook.Items, w => Assert.Equal("Mort", w.BookTitle));

        var search = Assert.Single((await Vocabulary.ListWordsAsync(new WordQuery { Search = "NNU" }, Ct)).Items);
        Assert.Equal("Listlessness.", search.ShortDefinition);
        Assert.Equal(DictionaryLookupStatus.Found, search.DefinitionStatus);
    }

    [Fact]
    public async Task Deleting_a_word_keeps_the_cached_definition()
    {
        var word = Success(await Vocabulary.AddWordAsync(new WordInput { Term = "ennui" }, Ct));

        Assert.True((await Vocabulary.DeleteWordAsync(word.Id, Ct)).IsSuccess);
        Failure(await Vocabulary.GetWordAsync(word.Id, Ct), ErrorType.NotFound);

        await using var db = Database.CreateContext();
        Assert.Single(await db.DictionaryEntries.ToListAsync(Ct));
    }
}
