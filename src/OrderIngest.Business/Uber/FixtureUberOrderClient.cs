using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace OrderIngest.Business.Uber;

public class FixtureUberOrderClient(IOptions<UberOptions> options, IHostEnvironment env)
    : IUberOrderClient
{
    public Task<string> GetOrderJsonAsync(string resourceHref, CancellationToken ct = default)
    {
        // Resolve against the content root so the fixture is found no matter
        // which directory the app (or a test host) was started from.
        var path = options.Value.FixturePath;
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(env.ContentRootPath, path);
        }

        return File.ReadAllTextAsync(path, ct);
    }
}
