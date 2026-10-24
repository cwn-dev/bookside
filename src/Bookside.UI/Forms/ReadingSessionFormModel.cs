using Bookside.Core.Books;
using Bookside.Core.Books.Services;

namespace Bookside.UI.Forms;

/// <summary>Mutable form state for <see cref="ReadingSessionInput"/>; property names match the input.</summary>
public sealed class ReadingSessionFormModel
{
    public ReadingStatus Status { get; set; } = ReadingStatus.Reading;

    public DateOnly? StartedOn { get; set; }

    public DateOnly? FinishedOn { get; set; }

    public int? Rating { get; set; }

    public string? Notes { get; set; }

    public static ReadingSessionFormModel From(ReadingSessionDto session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new ReadingSessionFormModel
        {
            Status = session.Status,
            StartedOn = session.StartedOn,
            FinishedOn = session.FinishedOn,
            Rating = session.Rating,
            Notes = session.Notes,
        };
    }

    /// <summary>A finish date only makes sense once the session is over, so it's dropped while Reading.</summary>
    public ReadingSessionInput ToInput() => new()
    {
        Status = Status,
        StartedOn = StartedOn,
        FinishedOn = Status == ReadingStatus.Reading ? null : FinishedOn,
        Rating = Rating,
        Notes = Notes,
    };
}
