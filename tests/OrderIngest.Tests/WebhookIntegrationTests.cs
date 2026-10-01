using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OrderIngest.Tests;

public class WebhookIntegrationTests
{
    internal static string Sign(string body) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(TestApiFactory.UberSecret),
            Encoding.UTF8.GetBytes(body)));

    internal static HttpContent JsonContent(string body) =>
        new StringContent(body, Encoding.UTF8, "application/json");

    internal static async Task<HttpResponseMessage> PostUberAsync(HttpClient client, string body)
    {
        var content = JsonContent(body);
        content.Headers.Add("X-Uber-Signature", Sign(body));
        return await client.PostAsync("/webhooks/orders", content);
    }

    /// <summary>Processing happens on a background service after the 200, so poll briefly.</summary>
    internal static async Task<JsonElement> WaitForOrderCountAsync(HttpClient client, int expected)
    {
        JsonElement orders = default;
        for (var i = 0; i < 50; i++)
        {
            orders = JsonDocument.Parse(
                await client.GetStringAsync("/orders")).RootElement.Clone();
            if (orders.GetArrayLength() == expected)
            {
                return orders;
            }

            await Task.Delay(100);
        }

        Assert.Fail($"Expected {expected} orders, found {orders.GetArrayLength()} after waiting");
        return default;
    }

    [Fact]
    public async Task UberWebhook_PostedTwice_YieldsExactlyOneRow()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        var body = TestFixtures.Read("uber-notification.json");

        var first = await PostUberAsync(client, body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(0, first.Content.Headers.ContentLength ?? 0);
        await WaitForOrderCountAsync(client, 1);

        var second = await PostUberAsync(client, body);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // Give the replay time to be (not) processed, then prove one row.
        await Task.Delay(750);
        var orders = JsonDocument.Parse(await client.GetStringAsync("/orders")).RootElement;
        var order = Assert.Single(orders.EnumerateArray());
        Assert.Equal("Uber", order.GetProperty("provider").GetString());
        Assert.Equal(1399, order.GetProperty("totalCents").GetInt64());
    }

    [Fact]
    public async Task UberWebhook_InvalidSignature_Returns401AndStoresNothing()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        var body = TestFixtures.Read("uber-notification.json");

        var content = JsonContent(body);
        content.Headers.Add("X-Uber-Signature", "0000000000000000");
        var response = await client.PostAsync("/webhooks/orders", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await Task.Delay(300);
        var orders = JsonDocument.Parse(await client.GetStringAsync("/orders")).RootElement;
        Assert.Equal(0, orders.GetArrayLength());
    }

    [Fact]
    public async Task UnrecognizedPayload_Returns400()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/webhooks/orders", JsonContent("""{"hello": "world"}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NonOrderUberEventType_Returns400()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/webhooks/orders", JsonContent("""{"event_type": "store.provisioned"}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DoorDashWebhook_IsStored_AndExposedWithInternalNamesOnly()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        var body = TestFixtures.Read("doordash-order.json");

        var response = await client.PostAsync("/webhooks/orders", JsonContent(body));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orders = await WaitForOrderCountAsync(client, 1);
        var summary = orders.EnumerateArray().Single();
        Assert.Equal("DoorDash", summary.GetProperty("provider").GetString());
        Assert.Equal(2300, summary.GetProperty("totalCents").GetInt64());

        var id = summary.GetProperty("id").GetInt64();
        var detailJson = await client.GetStringAsync($"/orders/{id}");
        var detail = JsonDocument.Parse(detailJson).RootElement;
        Assert.Equal("Kelley W.", detail.GetProperty("customer").GetProperty("name").GetString());
        Assert.Equal("Created", detail.GetProperty("status").GetString());

        // No provider field names may leak through the internal DTOs.
        Assert.DoesNotContain("consumer", detailJson);
        Assert.DoesNotContain("categories", detailJson);
        Assert.DoesNotContain("merchant_supplied_id", detailJson);
        Assert.DoesNotContain("eater", detailJson);
    }

    [Fact]
    public async Task BothFixtures_ProduceTwoRows_NewestFirst()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        await PostUberAsync(client, TestFixtures.Read("uber-notification.json"));
        await WaitForOrderCountAsync(client, 1);
        await client.PostAsync(
            "/webhooks/orders", JsonContent(TestFixtures.Read("doordash-order.json")));

        var orders = await WaitForOrderCountAsync(client, 2);
        var providers = orders.EnumerateArray()
            .Select(o => o.GetProperty("provider").GetString()!)
            .ToArray();
        Assert.Equal(["DoorDash", "Uber"], providers);
    }
}
