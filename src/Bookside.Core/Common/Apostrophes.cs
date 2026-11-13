using System.Buffers;

namespace Bookside.Core.Common;

/// <summary>
/// Apostrophe variants that search treats as one character: the straight
/// <c>'</c>, the curly <c>’</c> and <c>‘</c> (phone keyboards type these, and
/// imported titles often have them) and the modifier letter <c>ʼ</c>.
/// </summary>
public static class Apostrophes
{
    public const string Characters = "'‘’ʼ";

    private static readonly SearchValues<char> Values = SearchValues.Create(Characters);

    /// <summary>
    /// <paramref name="text"/> with every apostrophe variant replaced by
    /// <paramref name="replacement"/> (default the straight <c>'</c>). The
    /// length stays the same, so indexes into the result fit the original.
    /// </summary>
    public static string Normalize(string text, char replacement = '\'')
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.AsSpan().IndexOfAny(Values) < 0)
        {
            return text;
        }

        return string.Create(text.Length, (text, replacement), static (span, state) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                var c = state.text[i];
                span[i] = Values.Contains(c) ? state.replacement : c;
            }
        });
    }
}
