using Bookside.Core.Books.Services;
using Bookside.Core.Common;
using Bookside.Core.Vocabulary.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bookside.Api.Words;

internal static class WordsApi
{
    private const string BookField = nameof(ApiWordInput.Book);

    public static void Map(IEndpointRouteBuilder api) =>
        api.MapPost("/words", AddWordAsync);

    /// <summary>
    /// Adds a word: 201 with the new word. A word already on the list is not
    /// an error: 200 with the existing one, so callers can safely resend.
    /// </summary>
    private static async Task<IResult> AddWordAsync(
        ApiWordInput input,
        IVocabularyService vocabulary,
        ILibraryService library,
        CancellationToken cancellationToken)
    {
        var bookId = input.BookId;
        if (!string.IsNullOrWhiteSpace(input.Book))
        {
            if (bookId is not null)
            {
                return ApiResults.Problem(ServiceError.Validation(
                    BookField,
                    "Send either book or bookId, not both."));
            }

            var book = await FindBookByTitleAsync(library, input.Book, cancellationToken)
                .ConfigureAwait(false);
            if (!book.IsSuccess)
            {
                return ApiResults.Problem(book.Error);
            }

            bookId = book.Value;
        }

        var wordInput = new WordInput
        {
            Term = input.Term ?? string.Empty,
            Language = string.IsNullOrWhiteSpace(input.Language) ? "en" : input.Language,
            BookId = bookId,
            Context = input.Context,
            PageNumber = input.PageNumber,
            Notes = input.Notes,
        };

        var added = await vocabulary
            .AddWordAsync(wordInput, cancellationToken)
            .ConfigureAwait(false);
        if (added.IsSuccess)
        {
            return Results.Created($"/words/{added.Value.Id}", added.Value);
        }

        if (added.Error.Code == ErrorCodes.DuplicateWord
            && await vocabulary
                .FindWordAsync(wordInput.Term, wordInput.Language, cancellationToken)
                .ConfigureAwait(false) is { } existing)
        {
            return Results.Ok(existing);
        }

        return ApiResults.Problem(added.Error);
    }

    // shortcut: checks only the first 20 search hits for the exact title;
    // add a title lookup to ILibraryService if libraries get that big.
    private static async Task<Result<Guid>> FindBookByTitleAsync(
        ILibraryService library,
        string title,
        CancellationToken cancellationToken)
    {
        title = title.Trim();
        var hits = await library
            .ListBooksAsync(new BookQuery { Search = title, Take = 20 }, cancellationToken)
            .ConfigureAwait(false);

        // Any apostrophe matches any other, like the search.
        var wanted = Apostrophes.Normalize(title);
        var matches = hits.Items
            .Where(b => string.Equals(
                Apostrophes.Normalize(b.Title),
                wanted,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            1 => matches[0].Id,
            0 => ServiceError.Validation(BookField, $"No book titled \"{title}\"."),
            _ => ServiceError.Validation(
                BookField,
                $"{matches.Count} books are titled \"{title}\"; send bookId instead."),
        };
    }
}
