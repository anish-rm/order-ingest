using System.Net;
using System.Text.Json;
using OrderIngest.Business.Uber;

namespace OrderIngest.Tests;

/// <summary>
/// Covers the paths after the 200 has been sent: the Uber fetch failing
/// (retried, then dropped) and a detected-but-unmappable payload
/// (dropped without retry). Neither may write a row.
/// </summary>
public class ProcessingFailureTests
{
    private sealed class ThrowingUberClient : IUberOrderClient
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);

        public Task<string> GetOrderJsonAsync(string resourceHref, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            throw new IOException("simulated upstream outage");
        }
    }

    [Fact]
    public async Task UberFetchFails_IsRetriedThreeTimes_AndNoRowIsWritten()
    {
        var throwingClient = new ThrowingUberClient();
        using var factory = new TestApiFactory(throwingClient);
        using var client = factory.CreateClient();

        var response = await WebhookIntegrationTests.PostUberAsync(
            client, TestFixtures.Read("uber-notification.json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // 3 attempts with 200ms/400ms backoff finish well within this window.
        await WaitUntilAsync(() => throwingClient.Calls >= 3);
        await Task.Delay(300);

        Assert.Equal(3, throwingClient.Calls);
        var orders = JsonDocument.Parse(await client.GetStringAsync("/orders")).RootElement;
        Assert.Equal(0, orders.GetArrayLength());
    }

    [Fact]
    public async Task UnmappableDoorDashPayload_IsDroppedWithoutWritingARow()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        // Valid DoorDash shape for detection, but the order is missing every
        // field the mapper needs — processing throws KeyNotFoundException.
        var body = """{"event": {"type": "OrderCreate", "status": "NEW"}, "order": {}}""";
        var response = await client.PostAsync(
            "/webhooks/orders", WebhookIntegrationTests.JsonContent(body));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await Task.Delay(500);
        var orders = JsonDocument.Parse(await client.GetStringAsync("/orders")).RootElement;
        Assert.Equal(0, orders.GetArrayLength());
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 50; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(100);
        }

        Assert.Fail("Condition not met after waiting");
    }
}
