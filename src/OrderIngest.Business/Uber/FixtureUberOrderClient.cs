using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace OrderIngest.Business.Uber;

public class FixtureUberOrderClient(IOptions<UberOptions> options, IHostEnvironment env)
    : IUberOrderClient
{
    public Task<string> GetOrderJsonAsync(string resourceHref, CancellationToken ct = default)
    {
        var path = options.Value.FixturePath;
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(env.ContentRootPath, path);
        }

        return File.ReadAllTextAsync(path, ct);
    }
}
