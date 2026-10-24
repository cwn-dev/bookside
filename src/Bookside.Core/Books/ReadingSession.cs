using Bookside.Core.Common;

namespace Bookside.Core.Books;

/// <summary>One read-through of a book. A book read three times has three sessions.</summary>
public class ReadingSession : Entity
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int NotesMaxLength = 10_000;

    public Guid BookId { get; set; }

    public Book Book { get; set; } = null!;

    public DateOnly? StartedOn { get; set; }

    public DateOnly? FinishedOn { get; set; }

    public ReadingStatus Status { get; set; } = ReadingStatus.Reading;

    /// <summary>Optional, <see cref="MinRating"/>–<see cref="MaxRating"/>.</summary>
    public int? Rating { get; set; }

    public string? Notes { get; set; }
}
