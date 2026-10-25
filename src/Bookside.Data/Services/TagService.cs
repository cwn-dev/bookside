using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.Core.Common;
using Bookside.Core.Vocabulary;
using Microsoft.EntityFrameworkCore;

namespace Bookside.Data.Services;

/// <summary>
/// Each call uses its own short-lived context, so the service is safe for a
/// whole Blazor circuit.
/// </summary>
internal sealed class TagService(IDbContextFactory<BooksideDbContext> dbFactory) : ITagService
{
    public async Task<IReadOnlyList<TagDto>> ListTagsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await db.Tags.AsNoTracking()
            .OrderBy(t => t.NormalizedName)
            .Select(t => new TagDto
            {
                Id = t.Id,
                Name = t.Name,
                BookCount = t.Books.Count
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<IReadOnlyList<string>>> SetBookTagsAsync(
        Guid bookId,
        IReadOnlyList<string> names,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(names);

        var validator = new InputValidator();
        var desired = new Dictionary<string, string>(StringComparer.Ordinal); // normalized → name as typed

        foreach (var raw in names)
        {
            var name = raw?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            validator.Check(
                name.Length <= Tag.NameMaxLength,
                "Tags",
                $"Tag names must be at most {Tag.NameMaxLength} characters.");

            desired.TryAdd(TermNormalizer.Normalize(name), name);
        }

        if (!validator.IsValid)
        {
            return validator.ToError();
        }

        try
        {
            return await ApplyBookTagsAsync(
                bookId,
                desired,
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Another call created one of the new tags first; the retry finds it.
            return await ApplyBookTagsAsync(
                bookId,
                desired,
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<Result<TagDto>> RenameTagAsync(
        Guid tagId,
        TagInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var validator = new InputValidator();

        var name = validator.Required(
            nameof(TagInput.Name),
            input.Name,
            Tag.NameMaxLength);

        await using var db = await dbFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var tag = await db.Tags
            .SingleOrDefaultAsync(t => t.Id == tagId, cancellationToken)
            .ConfigureAwait(false);

        if (tag is null)
        {
            return TagNotFound(tagId);
        }

        if (!validator.IsValid)
        {
            return validator.ToError();
        }

        var normalized = TermNormalizer.Normalize(name);

        if (await db.Tags
            .AnyAsync(t => t.Id != tagId
                && t.NormalizedName == normalized, cancellationToken)
            .ConfigureAwait(false))
        {
            return DuplicateTag(name);
        }

        tag.Name = name;

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            return DuplicateTag(name);
        }

        var bookCount = await db.BookTags
            .CountAsync(bt => bt.TagId == tagId, cancellationToken)
            .ConfigureAwait(false);

        return new TagDto
        {
            Id = tag.Id,
            Name = tag.Name,
            BookCount = bookCount
        };
    }

    public async Task<Result> DeleteTagAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Book links cascade in the database.
        var deleted = await db.Tags
            .Where(t => t.Id == tagId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return deleted == 0
            ? TagNotFound(tagId)
            : Result.Success();
    }

    private static ServiceError TagNotFound(Guid tagId) =>
        ServiceError.NotFound(
            ErrorCodes.TagNotFound,
            $"Tag {tagId} was not found.");

    private static ServiceError DuplicateTag(string name) =>
        ServiceError.Conflict(
            ErrorCodes.DuplicateTag,
            $"There is already a tag called \"{name}\".");

    private async Task<Result<IReadOnlyList<string>>> ApplyBookTagsAsync(
        Guid bookId,
        Dictionary<string, string> desired,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var book = await db.Books
            .Include(b => b.Tags)
            .ThenInclude(bt => bt.Tag)
            .SingleOrDefaultAsync(b => b.Id == bookId, cancellationToken)
            .ConfigureAwait(false);

        if (book is null)
        {
            return ServiceError.NotFound(
                ErrorCodes.BookNotFound,
                $"Book {bookId} was not found.");
        }

        foreach (var link in book.Tags
            .Where(bt => !desired.ContainsKey(bt.Tag.NormalizedName))
            .ToList())
        {
            book.Tags.Remove(link);
            db.BookTags.Remove(link);
        }

        var missing = desired.Keys
            .Where(n => !book.Tags.Any(bt => bt.Tag.NormalizedName == n))
            .ToList();

        var existing = await db.Tags
            .Where(t => missing.Contains(t.NormalizedName))
            .ToDictionaryAsync(t => 
                t.NormalizedName,
                StringComparer.Ordinal,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var normalized in missing)
        {
            if (!existing.TryGetValue(normalized, out var tag))
            {
                tag = new Tag { Name = desired[normalized] };
                db.Tags.Add(tag);
            }

            // Fix-up adds the link to book.Tags.
            db.BookTags.Add(new BookTag
            {
                Book = book,
                BookId = book.Id,
                Tag = tag,
                TagId = tag.Id
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return book.Tags
            .OrderBy(bt => bt.Tag.NormalizedName, StringComparer.Ordinal)
            .Select(bt => bt.Tag.Name)
            .ToList();
    }
}
