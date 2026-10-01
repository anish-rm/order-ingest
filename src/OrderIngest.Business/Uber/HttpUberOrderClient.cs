using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace OrderIngest.Business.Uber;

/// <summary>
/// Production implementation: GET resource_href with an OAuth bearer token
/// (scope eats.order or eats.store.orders.read). Token acquisition and
/// refresh are out of scope for this demo; the token is read from config.
/// </summary>
public class HttpUberOrderClient(HttpClient http, IOptions<UberOptions> options) : IUberOrderClient
{
    public async Task<string> GetOrderJsonAsync(string resourceHref, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, resourceHref);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", options.Value.AccessToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
