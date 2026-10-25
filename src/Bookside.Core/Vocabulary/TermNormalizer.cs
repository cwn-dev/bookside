using System.Text;

namespace Bookside.Core.Vocabulary;

/// <summary>
/// Canonical form used for uniqueness and cache keys: NFKC, trimmed,
/// single-spaced, lower-case invariant.
/// </summary>
public static class TermNormalizer
{
    public static string Normalize(string term)
    {
        ArgumentNullException.ThrowIfNull(term);

        var collapsed = string
            .Join(' ', term.Normalize(NormalizationForm.FormKC)
            .Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return collapsed.ToLowerInvariant();
    }
}
