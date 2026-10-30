using Bookside.Core.Books.Lookup;
using Bookside.Core.Books.Services;
using Bookside.Core.Common;
using Bookside.Core.Vocabulary;
using Bookside.Core.Vocabulary.Lookup;
using Bookside.Core.Vocabulary.Services;
using Bookside.Data;
using Bookside.Data.Sqlite;
using Bunit.TestDoubles;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.UI.Tests;

/// <summary>
/// bUnit context wired to the production service registrations on a real, migrated temp SQLite file,
/// so components are tested against real validation and persistence.
/// </summary>
public abstract class ComponentTestBase : BunitContext, IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "bookside-ui-tests", Guid.NewGuid().ToString("N"));

    protected ComponentTestBase()
    {
        Services
            .AddBooksideSqlite(Path.Combine(_directory, "bookside.db"))
            .AddBooksideServices()
            .AddSingleton<IDictionaryLookupService>(Dictionary)
            .AddSingleton<IBookLookupService>(BookLookup);
    }

    protected static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    /// <summary>Unknown terms are NotFound; set <see cref="FakeDictionaryLookup.Unavailable"/> to simulate an outage.</summary>
    protected FakeDictionaryLookup Dictionary { get; } = new();

    /// <summary>
    /// Book metadata provider: <see cref="FakeBookLookup.Results"/> answer
    /// every search; covers are a tiny PNG.
    /// </summary>
    protected FakeBookLookup BookLookup { get; } = new();

    protected ILibraryService Library => Services.GetRequiredService<ILibraryService>();

    protected ITagService Tags => Services.GetRequiredService<ITagService>();

    protected IVocabularyService Vocabulary => Services.GetRequiredService<IVocabularyService>();

    protected BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    public async ValueTask InitializeAsync() => await Services.InitializeBooksideDatabaseAsync(Ct);

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    protected async Task<BookDetails> AddBookAsync(string title, params string[] authors) =>
        Success(await Library.AddBookAsync(new BookInput { Title = title, Authors = authors }, Ct));

    protected async Task<BookDetails> GetBookAsync(Guid id) => Success(await Library.GetBookAsync(id, Ct));

    protected async Task<WordDetails> AddWordAsync(string term, Guid? bookId = null, string? context = null, int? page = null) =>
        Success(await Vocabulary.AddWordAsync(new WordInput { Term = term, BookId = bookId, Context = context, PageNumber = page }, Ct));

    protected static T Success<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }
}

public sealed class FakeDictionaryLookup : IDictionaryLookupService
{
    private readonly Dictionary<string, DictionaryLookupResult> _entries = new(StringComparer.Ordinal);

    public string Source => "test";

    public bool Unavailable { get; set; }

    public Task<DictionaryLookupResult> LookupAsync(string term, string language = "en", CancellationToken cancellationToken = default) =>
        Unavailable
            ? throw new DictionaryLookupException("Simulated outage.")
            : Task.FromResult(_entries.TryGetValue(term, out var result) ? result : DictionaryLookupResult.NotFound());

    /// <param name="term">Normalized (lower-case), as the definition service looks it up.</param>
    public void Define(string term, string definition, string? example = null) => _entries[term] = new DictionaryLookupResult
    {
        Status = DictionaryLookupStatus.Found,
        Phonetic = "/test/",
        AudioUrl = "https://audio.example/" + term + ".mp3",
        Meanings =
        [
            new DictionaryMeaning
            {
                PartOfSpeech = "noun",
                Definitions = [new DictionaryDefinition { Text = definition, Example = example }],
            },
        ],
    };
}

public sealed class FakeBookLookup : IBookLookupService
{
    public string Source => "openlibrary";

    public IReadOnlyList<BookMetadata> Results { get; set; } = [];

    /// <summary>
    /// When set, cover downloads wait for this task, so a test can see the
    /// page while the download runs.
    /// </summary>
    public Task? CoverDownloadGate { get; set; }

    public Task<BookMetadata?> FindByIsbnAsync(
        string isbn13,
        CancellationToken cancellationToken = default) =>
            Task.FromResult(Results.Count > 0 ? Results[0] : null);

    public Task<IReadOnlyList<BookMetadata>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default) =>
            Task.FromResult(Results);

    public async Task<CoverInput> DownloadCoverAsync(
        Uri url,
        CancellationToken cancellationToken = default)
    {
        if (CoverDownloadGate is not null)
        {
            await CoverDownloadGate.WaitAsync(cancellationToken);
        }

        return new CoverInput
        {
            Content = new byte[] { 0x89, 0x50, 0x4E, 0x47 },
            ContentType = "image/png",
            SourceUrl = url.AbsoluteUri,
        };
    }
}
