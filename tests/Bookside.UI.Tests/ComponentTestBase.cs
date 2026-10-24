using Bookside.Core.Books.Services;
using Bookside.Core.Common;
using Bookside.Core.Vocabulary.Lookup;
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
            .AddSingleton<IDictionaryLookupService, UnavailableDictionary>();
    }

    protected static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    protected ILibraryService Library => Services.GetRequiredService<ILibraryService>();

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

    protected static T Success<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    /// <summary>Library pages never look words up; registered only because the services need one.</summary>
    private sealed class UnavailableDictionary : IDictionaryLookupService
    {
        public string Source => "test";

        public Task<DictionaryLookupResult> LookupAsync(string term, string language = "en", CancellationToken cancellationToken = default) =>
            throw new DictionaryLookupException("Not available in UI tests.");
    }
}
