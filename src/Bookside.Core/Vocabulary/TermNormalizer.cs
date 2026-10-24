using System.Text;

namespace Bookside.Core.Vocabulary;

/// <summary>Canonical form used for uniqueness and cache keys: NFKC, trimmed, single-spaced, lower-case invariant.</summary>
public static class TermNormalizer
{
    public static string Normalize(string term)
    {
        ArgumentNullException.ThrowIfNull(term);

        var collapsed = string.Join(' ', term.Normalize(NormalizationForm.FormKC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

#pragma warning disable CA1308 // Lower-case is the conventional canonical form for dictionary keys.
        return collapsed.ToLowerInvariant();
#pragma warning restore CA1308
    }
}
