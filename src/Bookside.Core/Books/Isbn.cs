namespace Bookside.Core.Books;

/// <summary>
/// ISBN cleaning and checksum validation.
/// Stored forms: 13 digits, or 9 digits + digit/X.
/// </summary>
public static class Isbn
{
    /// <summary>
    /// Removes hyphens and whitespace and upper-cases a trailing x.
    /// </summary>
    public static string Clean(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return string
            .Concat(input.Where(c => c != '-' && !char.IsWhiteSpace(c)))
            .ToUpperInvariant();
    }

    public static bool IsValidIsbn13(string isbn)
    {
        ArgumentNullException.ThrowIfNull(isbn);

        if (isbn.Length != 13 || !isbn.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;

        for (var i = 0; i < 13; i++)
        {
            sum += (isbn[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return sum % 10 == 0;
    }

    public static bool IsValidIsbn10(string isbn)
    {
        ArgumentNullException.ThrowIfNull(isbn);

        if (isbn.Length != 10
            || !isbn[..9].All(char.IsAsciiDigit)
            || !(char.IsAsciiDigit(isbn[9]) || isbn[9] == 'X'))
        {
            return false;
        }

        var sum = 0;

        for (var i = 0; i < 10; i++)
        {
            var value = isbn[i] == 'X' ? 10 : isbn[i] - '0';
            sum += value * (10 - i);
        }

        return sum % 11 == 0;
    }

    /// <summary>
    /// Converts a valid ISBN-10 to its 978-prefixed ISBN-13.
    /// </summary>
    public static string ToIsbn13(string isbn10)
    {
        if (!IsValidIsbn10(isbn10))
        {
            throw new ArgumentException("Not a valid ISBN-10.", nameof(isbn10));
        }

        var body = "978" + isbn10[..9];
        var sum = 0;

        for (var i = 0; i < 12; i++)
        {
            sum += (body[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return body + (char)('0' + ((10 - (sum % 10)) % 10));
    }
}
