using Bookside.Core.Common;

namespace Bookside.Core.Books.Services;

/// <summary>
/// Book tags. Names are unique case-insensitively; a tag is kept when no
/// book uses it, until deleted.
/// </summary>
public interface ITagService
{
    /// <summary>
    /// All tags, alphabetically, with how many books use each.
    /// </summary>
    Task<IReadOnlyList<TagDto>> ListTagsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the book's tags. Names are matched to existing tags
    /// case-insensitively; new names create tags. Returns the book's tag
    /// names, alphabetically.
    /// </summary>
    Task<Result<IReadOnlyList<string>>> SetBookTagsAsync(
        Guid bookId,
        IReadOnlyList<string> names,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Conflict if another tag already has the name; changing only the
    /// case is allowed.
    /// </summary>
    Task<Result<TagDto>> RenameTagAsync(
        Guid tagId,
        TagInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the tag from every book.
    /// </summary>
    Task<Result> DeleteTagAsync(
        Guid tagId,
        CancellationToken cancellationToken = default);
}

public sealed record TagInput
{
    /// <summary>
    /// Required; trimmed.
    /// </summary>
    public required string Name { get; init; }
}

public sealed record TagDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public int BookCount { get; init; }
}
