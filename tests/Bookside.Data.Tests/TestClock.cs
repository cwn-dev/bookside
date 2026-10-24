namespace Bookside.Data.Tests;

/// <summary>A settable clock. Each read advances it by 1 ms, so timestamps stamped in order stay in order.</summary>
public sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    private readonly Lock _lock = new();
    private DateTimeOffset _now = now;

    public TimeZoneInfo Zone { get; set; } = TimeZoneInfo.Utc;

    public override TimeZoneInfo LocalTimeZone => Zone;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            _now = _now.AddMilliseconds(1);
            return _now;
        }
    }

    public void Set(DateTimeOffset now)
    {
        lock (_lock)
        {
            _now = now;
        }
    }
}
