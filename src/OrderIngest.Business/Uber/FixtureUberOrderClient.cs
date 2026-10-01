using Microsoft.Extensions.Options;

namespace OrderIngest.Business.Uber;

public class FixtureUberOrderClient(IOptions<UberOptions> options) : IUberOrderClient
{
    public Task<string> GetOrderJsonAsync(string resourceHref, CancellationToken ct = default) =>
        File.ReadAllTextAsync(options.Value.FixturePath, ct);
}
