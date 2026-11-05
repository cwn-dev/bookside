using System.Text;
using Bookside.Core.Books;

namespace Bookside.Core.Search;

/// <summary>
/// Cuts a short excerpt of comment text around a search match.
/// </summary>
public static class SearchSnippet
{
    public const int DefaultMaxLength = 160;

    private const string Ellipsis = "…";

    /// <summary>
    /// About <paramref name="maxLength"/> characters of <paramref name="text"/>
    /// around the first case-insensitive match of <paramref name="query"/>,
    /// cut at spaces where possible, with <c>…</c> where text was left out.
    /// Inline tags are shown in display form first (<c>@[Jane Eyre]</c> →
    /// <c>@Jane Eyre</c>), so a cut never leaves half a tag's brackets.
    /// Whitespace runs, including line breaks, become single spaces. Null when
    /// the query only matches inside tags that aren't
    /// <see cref="CommentTagTrigger.Searchable"/> (or not at all); a blank
    /// query gives the start of the text.
    /// </summary>
    public static string? Around(
        string text,
        string query,
        int maxLength = DefaultMaxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 20);

        var (display, searchable) = ToDisplay(text);
        var term = query.Trim();
        var match = term.Length == 0
            ? -1
            : searchable.IndexOf(term, StringComparison.OrdinalIgnoreCase);

        if (term.Length > 0 && match < 0)
        {
            return null;
        }

        if (display.Length <= maxLength)
        {
            return display;
        }

        // Put the match about a third of the way in, so some lead-up shows.
        var start = match < 0
            ? 0
            : Math.Clamp(
                match - (maxLength - term.Length) / 3,
                0,
                display.Length - maxLength);
        var end = start + maxLength;

        // Move the cuts back to the nearest space, unless that would lose the
        // match or most of the excerpt.
        if (start > 0)
        {
            var space = display.IndexOf(' ', start);
            var limit = match < 0
                ? start + maxLength / 4
                : Math.Min(match, start + maxLength / 4);

            if (space >= 0 && space < limit)
            {
                start = space + 1;
            }
        }

        if (end < display.Length)
        {
            var space = display.LastIndexOf(' ', end);
            var limit = Math.Max(start + maxLength * 3 / 4, match + term.Length);

            if (space > limit)
            {
                end = space;
            }
        }

        var excerpt = display[start..end].Trim();
        return (start > 0 ? Ellipsis : string.Empty)
            + excerpt
            + (end < display.Length ? Ellipsis : string.Empty);
    }

    /// <summary>
    /// The display text with whitespace collapsed, and the same text with
    /// unsearchable tags blanked out (<c>\0</c>), index for index.
    /// </summary>
    private static (string Display, string Searchable) ToDisplay(string text)
    {
        var display = new StringBuilder(text.Length);
        var searchable = new StringBuilder(text.Length);
        var lastWasSpace = true; // also trims leading whitespace

        foreach (var segment in CommentTagSyntax.Parse(text))
        {
            var isTag = segment.TagType is not null;
            var part = isTag
                ? CommentTagSyntax.Display(segment.TagType!.Value, segment.TagName!)
                : segment.Text;
            var hidden = isTag && !CommentTagSyntax.IsSearchable(segment.TagType!.Value);

            foreach (var c in part)
            {
                var space = char.IsWhiteSpace(c);

                if (space && lastWasSpace)
                {
                    continue;
                }

                display.Append(space ? ' ' : c);
                searchable.Append(hidden ? '\0' : space ? ' ' : c);
                lastWasSpace = space;
            }
        }

        if (lastWasSpace && display.Length > 0)
        {
            display.Length--;
            searchable.Length--;
        }

        return (display.ToString(), searchable.ToString());
    }
}
