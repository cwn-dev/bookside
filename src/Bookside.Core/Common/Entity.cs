namespace Bookside.Core.Common;

/// <summary>
/// Base for aggregate-style entities. Ids are UUIDv7 (time-ordered) so they index well
/// and can be generated client-side; timestamps are stamped by the data layer on save.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
