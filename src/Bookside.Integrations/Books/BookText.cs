using System.Net;
using System.Text.RegularExpressions;

namespace Bookside.Integrations.Books;

/// <summary>
/// Turns provider descriptions into the plain text the book page shows
/// (paragraphs separated by a blank line, rendered with
/// <c>white-space: pre-line</c>).
/// </summary>
internal static partial class BookText
{
    /// <summary>
    /// Google's HTML (<c>p</c>, <c>br</c>, <c>i</c>, <c>b</c>, lists): block
    /// ends become line breaks, other tags are dropped, entities decoded.
    /// </summary>
    public static string? FromHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var text = LineBreakTag().Replace(html, "\n");
        text = ListItemTag().Replace(text, "\n• ");
        text = BlockEndTag().Replace(text, "\n\n");
        text = AnyTag().Replace(text, "");
        return Tidy(WebUtility.HtmlDecode(text));
    }

    /// <summary>
    /// Open Library's text: CRLF line breaks and some Markdown (emphasis,
    /// links, a "----" rule before "Also contained in" lists).
    /// </summary>
    public static string? FromMarkdown(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return null;
        }

        var text = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (Rule().Match(text) is { Success: true } rule)
        {
            text = text[..rule.Index];
        }

        text = LinkReference().Replace(text, "");
        text = Link().Replace(text, "$1");
        // Twice, for emphasis inside emphasis ("**From the *Times* …**").
        text = Emphasis().Replace(Emphasis().Replace(text, "$2"), "$2");
        return Tidy(text);
    }

    /// <summary>
    /// Trims each line, collapses runs of spaces and of blank lines.
    /// </summary>
    private static string? Tidy(string text)
    {
        var lines = text
            .Replace(' ', ' ')
            .Split('\n')
            .Select(l => Spaces().Replace(l, " ").Trim());

        var tidy = BlankLines().Replace(string.Join('\n', lines), "\n\n").Trim();
        return tidy.Length > 0 ? tidy : null;
    }

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakTag();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemTag();

    [GeneratedRegex(@"</(p|div|ul|ol|h[1-6]|blockquote)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEndTag();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"^\s*-{3,}\s*$", RegexOptions.Multiline)]
    private static partial Regex Rule();

    [GeneratedRegex(@"^\s*\[[^\]]+\]:\s*\S+\s*$", RegexOptions.Multiline)]
    private static partial Regex LinkReference();

    [GeneratedRegex(@"\[([^\]]+)\](?:\([^)]*\)|\[[^\]]*\])")]
    private static partial Regex Link();

    [GeneratedRegex(@"(\*{1,2}|_{2})(\S(?:.*?\S)?)\1")]
    private static partial Regex Emphasis();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();
}
