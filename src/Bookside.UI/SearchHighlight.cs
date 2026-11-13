using Bookside.Core.Common;

namespace Bookside.UI;

/// <summary>
/// A piece of text in a search result, marked when it matches the query.
/// </summary>
public readonly record struct HighlightSegment(string Text, bool IsMatch);

/// <summary>
/// Splits result text around the search query, for <c>&lt;mark&gt;</c>
/// highlighting.
/// </summary>
public static class SearchHighlight
{
    /// <summary>
    /// Every non-overlapping match of <paramref name="query"/> (trimmed,
    /// ordinal ignoring case, like SQLite's LIKE for ASCII; any apostrophe
    /// matches any other, like the search), with the text between; joined,
    /// the segments give <paramref name="text"/> back. A blank query gives
    /// the whole text, unmatched.
    /// </summary>
    public static IReadOnlyList<HighlightSegment> Split(string text, string? query)
    {
        ArgumentNullException.ThrowIfNull(text);

        var term = query?.Trim();

        if (string.IsNullOrEmpty(term) || text.Length == 0)
        {
            return text.Length == 0 ? [] : [new HighlightSegment(text, false)];
        }

        // Same length as the originals, so match indexes fit text.
        var haystack = Apostrophes.Normalize(text);
        term = Apostrophes.Normalize(term);
        var segments = new List<HighlightSegment>();
        var start = 0;

        while (start < text.Length)
        {
            var match = haystack.IndexOf(term, start, StringComparison.OrdinalIgnoreCase);

            if (match < 0)
            {
                break;
            }

            if (match > start)
            {
                segments.Add(new HighlightSegment(text[start..match], false));
            }

            segments.Add(new HighlightSegment(text.Substring(match, term.Length), true));
            start = match + term.Length;
        }

        if (start < text.Length)
        {
            segments.Add(new HighlightSegment(text[start..], false));
        }

        return segments;
    }
}
