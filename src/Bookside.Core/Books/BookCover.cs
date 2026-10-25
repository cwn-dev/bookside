namespace Bookside.Core.Books;

/// <summary>
/// Cover image stored in the database (1:1 with <see cref="Book"/>, keyed by BookId).
/// </summary>
public class BookCover
{
    public const int MaxSizeBytes = 2 * 1024 * 1024;
    public const int SourceUrlMaxLength = 2000;

    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(
        [
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/gif"
        ], StringComparer.OrdinalIgnoreCase);

    public Guid BookId { get; set; }

    public Book Book { get; set; } = null!;

    public required byte[] Content { get; set; }

    /// <summary>
    /// MimeType
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// Hex SHA-256 of <see cref="Content"/>; doubles as an HTTP ETag when served.
    /// </summary>
    public required string Sha256 { get; set; }

    /// <summary>
    /// Where the image came from, if downloaded (informational only).
    /// </summary>
    public string? SourceUrl { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
