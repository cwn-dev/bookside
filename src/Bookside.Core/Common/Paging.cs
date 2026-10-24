namespace Bookside.Core.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);

public static class Paging
{
    public const int DefaultTake = 50;
    public const int MaxTake = 200;

    /// <summary>Clamps caller-supplied paging to sane bounds instead of failing.</summary>
    public static (int Skip, int Take) Clamp(int skip, int take) =>
        (Math.Max(0, skip), take <= 0 ? DefaultTake : Math.Min(take, MaxTake));
}
