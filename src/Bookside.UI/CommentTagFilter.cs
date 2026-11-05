using System.Globalization;
using Bookside.Core.Books;
using Bookside.Core.Books.Services;
using Bookside.Core.Vocabulary;

namespace Bookside.UI;

/// <summary>
/// A tag used in a book's comments, as a reading log filter chip.
/// <see cref="Key"/> is its stored form (<c>@[Jane Eyre]</c>, <c>#grim</c>,
/// <c>p.42</c>), used as the <c>?tag=</c> value.
/// </summary>
public sealed record CommentTagChip(CommentTagType Type, string Name, string Key)
{
    public string Display => CommentTagSyntax.Display(Type, Name);
}

/// <summary>
/// Tag filtering for the reading log. Tags match like they are stored:
/// by type and normalized name, so <c>@jane</c> and <c>@[Jane]</c> are one.
/// </summary>
public static class CommentTagFilter
{
    /// <summary>
    /// Every tag in <paramref name="comments"/> once (first spelling wins):
    /// people, then general tags, by name; then pages in page order.
    /// </summary>
    public static IReadOnlyList<CommentTagChip> Chips(IEnumerable<BookCommentDto> comments) =>
        comments
            .SelectMany(c => TagsIn(c.Text))
            .DistinctBy(t => (t.Type, Normalized: TermNormalizer.Normalize(t.Name)))
            .OrderBy(t => t.Type)
            .ThenBy(t => t.Type == CommentTagType.Page
                ? int.Parse(t.Name, CultureInfo.InvariantCulture)
                : 0)
            .ThenBy(t => TermNormalizer.Normalize(t.Name), StringComparer.Ordinal)
            .Select(t => new CommentTagChip(t.Type, t.Name, CommentTagSyntax.Format(t.Type, t.Name)))
            .ToList();

    /// <summary>
    /// The tag a <c>?tag=</c> value names, or null when it isn't exactly one tag.
    /// </summary>
    public static CommentTagChip? Parse(string? key) =>
        key is null
            || CommentTagSyntax.Parse(key) is not [{ TagType: { } type, TagName: { } name }]
            ? null
            : new CommentTagChip(type, name, CommentTagSyntax.Format(type, name));

    public static bool Matches(CommentTagChip chip, CommentTagChip other) =>
        chip.Type == other.Type
        && TermNormalizer.Normalize(chip.Name) == TermNormalizer.Normalize(other.Name);

    public static bool Has(BookCommentDto comment, CommentTagChip tag) =>
        TagsIn(comment.Text).Any(t => Matches(t, tag));

    private static IEnumerable<CommentTagChip> TagsIn(string text) =>
        CommentTagSyntax.Parse(text)
            .Where(s => s.TagType is not null)
            .Select(s => new CommentTagChip(
                s.TagType!.Value,
                s.TagName!,
                CommentTagSyntax.Format(s.TagType.Value, s.TagName!)));
}
