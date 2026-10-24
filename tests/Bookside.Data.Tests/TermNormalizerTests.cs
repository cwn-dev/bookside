using Bookside.Core.Vocabulary;

namespace Bookside.Data.Tests;

public sealed class TermNormalizerTests
{
    [Theory]
    [InlineData("Word", "word")]
    [InlineData("  padded  ", "padded")]
    [InlineData("ad   hoc", "ad hoc")]
    [InlineData("ﬁnesse", "finesse")] // NFKC folds the "fi" ligature
    public void Normalizes(string input, string expected) =>
        Assert.Equal(expected, TermNormalizer.Normalize(input));
}
