using Bookside.Core.Books;
using Bookside.Core.Common;

namespace Bookside.Data.Tests;

public sealed class IsbnTests
{
    [Theory]
    [InlineData("978-0-06-085398-3", "9780060853983")]
    [InlineData(" 0 8044 2957 x ", "080442957X")]
    public void Clean_strips_separators_and_uppercases_x(string input, string expected) =>
        Assert.Equal(expected, Isbn.Clean(input));

    [Theory]
    [InlineData("9780060853983", true)]
    [InlineData("9780060853984", false)]
    [InlineData("978006085398", false)]
    [InlineData("978006085398A", false)]
    public void Validates_isbn13_checksum(string isbn, bool valid) => Assert.Equal(valid, Isbn.IsValidIsbn13(isbn));

    [Theory]
    [InlineData("0060853980", true)]
    [InlineData("080442957X", true)]
    [InlineData("0804429570", false)]
    [InlineData("X804429570", false)]
    public void Validates_isbn10_checksum(string isbn, bool valid) => Assert.Equal(valid, Isbn.IsValidIsbn10(isbn));

    [Theory]
    [InlineData("0060853980", "9780060853983")]
    [InlineData("080442957X", "9780804429573")]
    public void Converts_isbn10_to_isbn13(string isbn10, string isbn13)
    {
        Assert.Equal(isbn13, Isbn.ToIsbn13(isbn10));
        Assert.True(Isbn.IsValidIsbn13(isbn13));
    }
}

public sealed class ResultTests
{
    [Fact]
    public void Values_and_errors_convert_implicitly()
    {
        Result<int> success = 42;
        Result<int> failure = ServiceError.NotFound("thing.not_found", "Nope.");

        Assert.True(success.IsSuccess);
        Assert.Equal(42, success.Value);
        Assert.False(failure.IsSuccess);
        Assert.Equal("thing.not_found", failure.Error.Code);
        Assert.Throws<InvalidOperationException>(() => failure.Value);
    }

    [Fact]
    public void Paging_is_clamped()
    {
        Assert.Equal((0, Paging.DefaultTake), Paging.Clamp(-5, 0));
        Assert.Equal((10, Paging.MaxTake), Paging.Clamp(10, 10_000));
    }
}
