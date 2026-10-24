namespace Bookside.Data.Services;

/// <summary>
/// Builds patterns for EF.Functions.Like with <see cref="EscapeCharacter"/>. Case-insensitivity comes from the
/// provider (SQLite LIKE ignores ASCII case; non-ASCII letters match case-sensitively).
/// </summary>
internal static class LikePattern
{
    public const string EscapeCharacter = "\\";

    public static string Contains(string value) => $"%{Escape(value)}%";

    public static string Exact(string value) => Escape(value);

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
