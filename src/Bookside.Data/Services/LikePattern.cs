using Bookside.Core.Common;

namespace Bookside.Data.Services;

/// <summary>
/// Builds patterns for EF.Functions.Like with <see cref="EscapeCharacter"/>.
/// Case-insensitivity comes from the provider (SQLite LIKE ignores ASCII case;
/// non-ASCII letters match case-sensitively). In search patterns any
/// apostrophe matches any other (see <see cref="Apostrophes"/>).
/// </summary>
internal static class LikePattern
{
    public const string EscapeCharacter = "\\";

    public static string Contains(string value) => $"%{SearchTerm(value)}%";

    public static string StartsWith(string value) => $"{SearchTerm(value)}%";

    public static string Exact(string value) => Escape(value);

    // An apostrophe becomes "_" (any one character), so ' finds ’ and back.
    private static string SearchTerm(string value) =>
        Apostrophes.Normalize(Escape(value), '_');

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
