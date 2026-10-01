namespace OrderIngest.Business.Uber;

/// <summary>
/// Fetches the full order behind a webhook's resource_href.
/// FixtureUberOrderClient serves a local fixture for the demo;
/// HttpUberOrderClient is the production implementation. Which one is
/// registered is a single config switch (Uber:OrderClientMode).
/// </summary>
public interface IUberOrderClient
{
    Task<string> GetOrderJsonAsync(string resourceHref, CancellationToken ct = default);
}
