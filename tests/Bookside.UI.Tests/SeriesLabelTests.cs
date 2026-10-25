namespace Bookside.UI.Tests;

public sealed class SeriesLabelTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData(null, 3.0, null)]
    [InlineData("Discworld", null, "Discworld")]
    [InlineData("Discworld", 3.0, "Discworld #3")]
    [InlineData("Discworld", 2.5, "Discworld #2.5")]
    [InlineData("Discworld", 0.0, "Discworld #0")]
    public void Formats_name_and_optional_number(string? series, double? number, string? expected) =>
        Assert.Equal(expected, SeriesLabel.Format(series, number));
}
