using Bookside.Core.Books.Services;
using Bookside.Core.Common;
using Bookside.Core.Home.Services;
using Bookside.Core.Vocabulary;
using Bookside.Core.Vocabulary.Lookup;
using Bookside.Core.Vocabulary.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.Data.Tests.Services;

/// <summary>
/// Real migrated SQLite database + the production service registrations + a fake dictionary
/// (<see cref="Dictionary"/>, optionally followed by fallback providers).
/// </summary>
public abstract class ServiceTestBase : IAsyncLifetime
{
    private readonly SqliteTestDatabase _database;
    private AsyncServiceScope _scope;

    /// <param name="fallbackSources">Extra fake providers registered after <see cref="Dictionary"/>, in order.</param>
    protected ServiceTestBase(params string[] fallbackSources)
    {
        Fallbacks = [.. fallbackSources.Select(source => new FakeDictionaryLookupService(source))];
        _database = new SqliteTestDatabase
        {
            ConfigureServices = services =>
            {
                ConfigureServices(services);
                services.AddBooksideServices().AddSingleton<IDictionaryLookupService>(Dictionary);
                foreach (var fallback in Fallbacks)
                {
                    services.AddSingleton<IDictionaryLookupService>(fallback);
                }
            },
        };
    }

    /// <summary>Runs before the production registrations, e.g. to register a <see cref="TestClock"/> as the TimeProvider.</summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected FakeDictionaryLookupService Dictionary { get; } = new();

    protected IReadOnlyList<FakeDictionaryLookupService> Fallbacks { get; }

    protected SqliteTestDatabase Database => _database;

    protected ILibraryService Library => _scope.ServiceProvider.GetRequiredService<ILibraryService>();

    protected ITagService Tags => _scope.ServiceProvider.GetRequiredService<ITagService>();

    protected IVocabularyService Vocabulary => _scope.ServiceProvider.GetRequiredService<IVocabularyService>();

    protected IHomeService Home => _scope.ServiceProvider.GetRequiredService<IHomeService>();

    protected IDefinitionService Definitions => _scope.ServiceProvider.GetRequiredService<IDefinitionService>();

    /// <summary>
    /// Needs an <see cref="Core.Books.Lookup.IBookLookupService"/> registered
    /// in <see cref="ConfigureServices"/>.
    /// </summary>
    protected IBookMetadataService Metadata =>
        _scope.ServiceProvider.GetRequiredService<IBookMetadataService>();

    public async ValueTask InitializeAsync()
    {
        await _database.InitializeAsync();
        _scope = _database.Services.CreateAsyncScope();
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _database.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected async Task<BookDetails> AddBookAsync(string title, params string[] authors) =>
        Success(await Library.AddBookAsync(new BookInput { Title = title, Authors = authors }, Ct));

    protected static T Success<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    protected static ServiceError Failure(Result result, ErrorType type)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(type, result.Error.Type);
        return result.Error;
    }
}

public sealed class FakeDictionaryLookupService(string source = "fake") : IDictionaryLookupService
{
    public string Source => source;

    public Dictionary<string, DictionaryLookupResult> Entries { get; } = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public List<string> Calls { get; } = [];

    public Task<DictionaryLookupResult> LookupAsync(string term, string language = "en", CancellationToken cancellationToken = default)
    {
        Calls.Add(term);
        if (Unavailable)
        {
            throw new DictionaryLookupException($"Simulated {source} outage.");
        }

        return Task.FromResult(Entries.TryGetValue(term, out var result) ? result : DictionaryLookupResult.NotFound());
    }

    public void Define(string term, string definition) => Entries[term] = new DictionaryLookupResult
    {
        Status = DictionaryLookupStatus.Found,
        Phonetic = "/test/",
        Meanings =
        [
            new DictionaryMeaning
            {
                PartOfSpeech = "noun",
                Definitions = [new DictionaryDefinition { Text = definition }],
            },
        ],
    };
}
