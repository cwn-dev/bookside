using System.Buffers;
using System.Globalization;

namespace Bookside.Core.Books;

/// <summary>
/// One piece of a comment's text: plain text, or an inline tag when
/// <see cref="TagType"/> is set. <see cref="Text"/> is the source text either way.
/// </summary>
public sealed record CommentTextSegment(
    string Text,
    CommentTagType? TagType = null,
    string? TagName = null);

/// <summary>
/// What a trigger starts: the tag type, and whether global search matches text
/// inside such tags.
/// </summary>
public sealed record CommentTagTrigger(CommentTagType Type, bool Searchable);

/// <summary>
/// A tag being typed: the trigger at <see cref="Start"/> up to the caret, with
/// <see cref="Query"/> typed so far (without the trigger or the opening bracket).
/// </summary>
public sealed record OpenCommentTag(int Start, CommentTagType Type, string Query);

/// <summary>
/// Inline tags in comment text: a trigger followed by <c>[any name]</c> or a
/// bare word, e.g. <c>@[Jane Eyre]</c>, <c>@Jane</c>, <c>#[slow burn]</c>,
/// <c>#theme</c>; pages are <c>p.42</c> or <c>p. 42</c>. A trigger only counts
/// at the start or after a character that isn't a letter or digit (so
/// <c>me@x.com</c> and <c>chap.5</c> aren't tags), and a bare word must start
/// with a letter (so <c>#1</c> isn't).
/// </summary>
public static class CommentTagSyntax
{
    // Bare queries end after this many words, so the picker doesn't follow the
    // rest of the sentence.
    private const int MaxOpenWords = 4;

    private const int MaxPageDigits = 6;

    /// <summary>
    /// Triggers and what each one starts. Hard-coded for now; user-defined
    /// triggers would replace this map.
    /// </summary>
    public static IReadOnlyDictionary<string, CommentTagTrigger> Triggers { get; } =
        new Dictionary<string, CommentTagTrigger>
        {
            ["@"] = new(CommentTagType.Person, Searchable: true),
            ["#"] = new(CommentTagType.General, Searchable: true),
            ["p."] = new(CommentTagType.Page, Searchable: false),
        };

    // Triggers as an array: a foreach over the IReadOnlyDictionary boxes its
    // enumerator on every candidate character. Declared after Triggers, which
    // it reads during static initialization.
    private static readonly KeyValuePair<string, CommentTagTrigger>[] TriggerList =
        [.. Triggers];

    // First character of each trigger, so Parse can jump straight to the next
    // place a tag could start.
    private static readonly SearchValues<char> TriggerStarts =
        SearchValues.Create([.. TriggerList.Select(t => t.Key[0]).Distinct()]);

    public static string TriggerFor(CommentTagType type) =>
        Triggers.First(t => t.Value.Type == type).Key;

    public static bool IsSearchable(CommentTagType type) =>
        Triggers.Values.First(t => t.Type == type).Searchable;

    /// <summary>
    /// The stored form of a tag: <c>@[Jane Eyre]</c> (always bracketed), or
    /// <c>p.42</c> for a page.
    /// </summary>
    public static string Format(CommentTagType type, string name) =>
        type == CommentTagType.Page
            ? $"{TriggerFor(type)}{name}"
            : $"{TriggerFor(type)}[{name}]";

    /// <summary>
    /// How a tag reads on screen: <c>@Jane Eyre</c>, <c>#theme</c>, <c>p. 42</c>.
    /// </summary>
    public static string Display(CommentTagType type, string name) =>
        type == CommentTagType.Page
            ? $"{TriggerFor(type)} {name}"
            : $"{TriggerFor(type)}{name}";

    /// <summary>
    /// Splits <paramref name="text"/> into plain text and tag segments, in order;
    /// joining every segment's <see cref="CommentTextSegment.Text"/> gives the
    /// input back.
    /// </summary>
    public static IReadOnlyList<CommentTextSegment> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var segments = new List<CommentTextSegment>();
        var plainStart = 0;
        var i = 0;

        while (i < text.Length)
        {
            // Vectorised search for the next trigger's first character;
            // everything before it is plain text.
            var next = text.AsSpan(i).IndexOfAny(TriggerStarts);

            if (next < 0)
            {
                break;
            }

            i += next;

            if (!TryReadTag(text, i, out var type, out var name, out var length))
            {
                i++;
                continue;
            }

            if (i > plainStart)
            {
                segments.Add(new CommentTextSegment(text[plainStart..i]));
            }

            segments.Add(new CommentTextSegment(text.Substring(i, length), type, name));
            i += length;
            plainStart = i;
        }

        if (plainStart < text.Length)
        {
            segments.Add(new CommentTextSegment(text[plainStart..]));
        }

        return segments;
    }

    /// <summary>
    /// The tag being typed just before <paramref name="caret"/>, or null: the
    /// nearest valid trigger with no newline or <c>]</c> between it and the
    /// caret. Bracketed queries may hold any words; bare ones at most a few;
    /// page queries are digits (after an optional space).
    /// </summary>
    public static OpenCommentTag? FindOpen(string text, int caret)
    {
        ArgumentNullException.ThrowIfNull(text);

        caret = Math.Clamp(caret, 0, text.Length);
        var earliest = Math.Max(0, caret - CommentTag.NameMaxLength - 2);

        for (var i = caret - 1; i >= earliest; i--)
        {
            var c = text[i];

            if (c is '\n' or ']')
            {
                return null;
            }

            var trigger = Triggers.Keys.FirstOrDefault(t =>
                i + t.Length <= caret
                && text.AsSpan(i).StartsWith(t, StringComparison.Ordinal));

            if (trigger is null || !IsBoundary(text, i))
            {
                continue;
            }

            var type = Triggers[trigger].Type;
            var query = text[(i + trigger.Length)..caret];

            if (type == CommentTagType.Page)
            {
                var digits = query.StartsWith(' ') ? query[1..] : query;

                return digits.Length <= MaxPageDigits && digits.All(char.IsAsciiDigit)
                    ? new OpenCommentTag(i, type, digits)
                    : null;
            }

            if (query.StartsWith('['))
            {
                return new OpenCommentTag(i, type, query[1..]);
            }

            if (query.Length > 0 && !char.IsLetter(query[0]))
            {
                return null;
            }

            return query.Split(' ').Length <= MaxOpenWords
                ? new OpenCommentTag(i, type, query)
                : null;
        }

        return null;
    }

    /// <summary>
    /// <paramref name="query"/> with tags typed as they display turned into
    /// their stored, bracketed start (<c>@Fred Test</c> → <c>@[Fred Test</c>),
    /// so a plain text search of comments finds them. Other text is unchanged.
    /// </summary>
    public static string ToStoredSearch(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = new System.Text.StringBuilder(query.Length + 2);

        for (var i = 0; i < query.Length; i++)
        {
            result.Append(query[i]);

            var isNameTrigger = Triggers.TryGetValue(query[i].ToString(), out var trigger)
                && trigger.Type != CommentTagType.Page;

            if (isNameTrigger
                && IsBoundary(query, i)
                && i + 1 < query.Length
                && char.IsLetter(query[i + 1]))
            {
                result.Append('[');
            }
        }

        return result.ToString();
    }

    private static bool IsBoundary(string text, int index) =>
        index == 0 || !char.IsLetterOrDigit(text[index - 1]);

    private static bool TryReadTag(
        string text,
        int start,
        out CommentTagType type,
        out string name,
        out int length)
    {
        type = default;
        name = "";
        length = 0;

        if (!IsBoundary(text, start))
        {
            return false;
        }

        foreach (var (trigger, definition) in TriggerList)
        {
            if (!text.AsSpan(start).StartsWith(trigger, StringComparison.Ordinal))
            {
                continue;
            }

            type = definition.Type;
            var end = type == CommentTagType.Page
                ? ReadPage(text, start + trigger.Length, out name)
                : ReadName(text, start + trigger.Length, out name);
            length = end - start;
            return end > 0;
        }

        return false;
    }

    /// <summary>
    /// Reads <c>[name]</c> or a bare word at <paramref name="index"/>; returns
    /// the end index, or 0 when there is no valid name.
    /// </summary>
    private static int ReadName(string text, int index, out string name)
    {
        name = "";

        if (index >= text.Length)
        {
            return 0;
        }

        int end;

        if (text[index] == '[')
        {
            var close = text.IndexOfAny([']', '\n'], index + 1);

            if (close < 0 || text[close] != ']')
            {
                return 0;
            }

            name = text[(index + 1)..close].Trim();
            end = close + 1;
        }
        else
        {
            if (!char.IsLetter(text[index]))
            {
                return 0;
            }

            end = index;

            while (end < text.Length
                && (char.IsLetterOrDigit(text[end]) || text[end] is '-' or '_'))
            {
                end++;
            }

            name = text[index..end];
        }

        return name.Length is > 0 and <= CommentTag.NameMaxLength ? end : 0;
    }

    /// <summary>
    /// Reads an optional space and a page number (leading zeros dropped) at
    /// <paramref name="index"/>; returns the end index, or 0 when there is none.
    /// </summary>
    private static int ReadPage(string text, int index, out string name)
    {
        name = "";

        if (index < text.Length && text[index] == ' ')
        {
            index++;
        }

        var end = index;

        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        if (end == index
            || end - index > MaxPageDigits
            || (end < text.Length && char.IsLetter(text[end])))
        {
            return 0;
        }

        var page = int.Parse(text.AsSpan(index, end - index), CultureInfo.InvariantCulture);

        if (page == 0)
        {
            return 0;
        }

        name = page.ToString(CultureInfo.InvariantCulture);
        return end;
    }
}
