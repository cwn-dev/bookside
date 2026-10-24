namespace Bookside.Core.Home.Services;

/// <summary>Everything the home page shows, in one call (one round trip for a future API client).</summary>
public interface IHomeService
{
    Task<HomeSummary> GetHomeAsync(CancellationToken cancellationToken = default);
}
