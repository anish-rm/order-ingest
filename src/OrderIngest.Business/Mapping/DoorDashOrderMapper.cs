using System.Text.Json;
using OrderIngest.Domain;

namespace OrderIngest.Business.Mapping;

public static class DoorDashOrderMapper
{
    /// <summary>
    /// Maps a DoorDash Marketplace webhook ({ event, order }) to the
    /// internal Order. Items are flattened across categories; the total is
    /// subtotal + tax (both integer cents). The payload carries no currency,
    /// so USD is assumed (documented in CLAUDE.md).
    /// </summary>
    public static Order Map(string webhookJson, DateTimeOffset receivedAt)
    {
        using var doc = JsonDocument.Parse(webhookJson);
        var root = doc.RootElement;

        var @event = root.GetProperty("event");
        var order = root.GetProperty("order");

        var consumer = order.GetProperty("consumer");
        var name = string.Join(' ', new[]
        {
            consumer.TryGetProperty("first_name", out var first) ? first.GetString() : null,
            consumer.TryGetProperty("last_name", out var last) ? last.GetString() : null,
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

        var customer = new CustomerInfo
        {
            Name = name,
            Phone = consumer.TryGetProperty("phone", out var phone) ? phone.GetString() : null,
            Email = consumer.TryGetProperty("email", out var email) ? email.GetString() : null,
        };

        var lineItems = new List<LineItem>();
        foreach (var category in order.GetProperty("categories").EnumerateArray())
        {
            foreach (var item in category.GetProperty("items").EnumerateArray())
            {
                lineItems.Add(new LineItem
                {
                    Name = item.GetProperty("name").GetString() ?? "",
                    Quantity = item.GetProperty("quantity").GetInt32(),
                    PriceCents = item.GetProperty("price").GetInt64(),
                });
            }
        }

        var eventType = @event.GetProperty("type").GetString() ?? "";
        var eventStatus = @event.TryGetProperty("status", out var status)
            ? status.GetString() ?? ""
            : "";

        return new Order
        {
            Provider = OrderProvider.DoorDash,
            ExternalOrderId = order.GetProperty("id").GetString() ?? "",
            Status = MapStatus(eventType, eventStatus),
            RawStatus = $"{eventType}/{eventStatus}",
            Customer = customer,
            LineItems = lineItems,
            TotalCents = order.GetProperty("subtotal").GetInt64()
                + order.GetProperty("tax").GetInt64(),
            Currency = "USD",
            RawPayload = webhookJson,
            ReceivedAt = receivedAt,
            LastUpdatedAt = receivedAt,
        };
    }

    /// <summary>The order integration docs define OrderCreate with status NEW.</summary>
    public static OrderStatus MapStatus(string eventType, string eventStatus) =>
        (eventType, eventStatus) switch
        {
            ("OrderCreate", _) => OrderStatus.Created,
            _ => OrderStatus.Unknown,
        };
}
