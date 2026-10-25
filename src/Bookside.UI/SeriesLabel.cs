using System.Globalization;

namespace Bookside.UI;

/// <summary>
/// Display text for a book's series, e.g. "Discworld #3".
/// </summary>
public static class SeriesLabel
{
    public static string? Format(string? series, double? number) =>
        series is null ? null
        : number is double n ? $"{series} #{n.ToString("0.##", CultureInfo.InvariantCulture)}"
        : series;
}
