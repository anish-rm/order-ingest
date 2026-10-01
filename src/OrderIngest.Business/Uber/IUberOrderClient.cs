namespace OrderIngest.Business.Uber;

/// <summary>
/// Fetches the full order behind a webhook's resource_href.
/// FixtureUberOrderClient serves a local fixture for the demo;
/// HttpUberOrderClient is the production implementation.
/// </summary>
public interface IUberOrderClient
{
    Task<string> GetOrderJsonAsync(string resourceHref, CancellationToken ct = default);
}
