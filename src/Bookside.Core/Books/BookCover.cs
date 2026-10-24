namespace Bookside.Core.Books;

/// <summary>Cover image stored in the database (1:1 with <see cref="Book"/>, keyed by BookId).</summary>
public class BookCover
{
    /// <summary>Upper bound enforced by the database layer's validation; covers are typically 50–500 KB.</summary>
    public const int MaxSizeBytes = 2 * 1024 * 1024;

    public Guid BookId { get; set; }

    public Book Book { get; set; } = null!;

#pragma warning disable CA1819 // Byte array is the natural shape for a BLOB column.
    public required byte[] Content { get; set; }
#pragma warning restore CA1819

    /// <summary>e.g. image/jpeg, image/png, image/webp.</summary>
    public required string ContentType { get; set; }

    /// <summary>Hex SHA-256 of <see cref="Content"/>; doubles as an HTTP ETag when served.</summary>
    public required string Sha256 { get; set; }

    /// <summary>Where the image came from, if downloaded (informational only).</summary>
    public string? SourceUrl { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
