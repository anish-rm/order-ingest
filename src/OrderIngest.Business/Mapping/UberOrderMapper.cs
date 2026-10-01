using System.Text.Json;
using OrderIngest.Domain;

namespace OrderIngest.Business.Mapping;

public static class UberOrderMapper
{
    /// <summary>
    /// Maps an Uber Eats Get Order (v2) response to the internal Order.
    /// All Uber money amounts are already integer cents.
    /// </summary>
    public static Order Map(string orderJson, DateTimeOffset receivedAt)
    {
        using var doc = JsonDocument.Parse(orderJson);
        var root = doc.RootElement;

        var eater = root.GetProperty("eater");
        var customer = new CustomerInfo
        {
            Name = eater.GetProperty("first_name").GetString() ?? "",
            Phone = eater.TryGetProperty("phone", out var phone) ? phone.GetString() : null,
        };

        var lineItems = new List<LineItem>();
        foreach (var item in root.GetProperty("cart").GetProperty("items").EnumerateArray())
        {
            lineItems.Add(new LineItem
            {
                Name = item.GetProperty("title").GetString() ?? "",
                Quantity = item.GetProperty("quantity").GetInt32(),
                PriceCents = item.GetProperty("price").GetProperty("total_price")
                    .GetProperty("amount").GetInt64(),
            });
        }

        var total = root.GetProperty("payment").GetProperty("charges").GetProperty("total");
        var rawStatus = root.GetProperty("current_state").GetString() ?? "";

        return new Order
        {
            Provider = OrderProvider.Uber,
            ExternalOrderId = root.GetProperty("id").GetString() ?? "",
            Status = MapStatus(rawStatus),
            RawStatus = rawStatus,
            Customer = customer,
            LineItems = lineItems,
            TotalCents = total.GetProperty("amount").GetInt64(),
            Currency = total.GetProperty("currency_code").GetString() ?? "USD",
            RawPayload = orderJson,
            ReceivedAt = receivedAt,
            LastUpdatedAt = receivedAt,
        };
    }

    /// <summary>Documented current_state values: CREATED, ACCEPTED, DENIED, FINISHED, CANCELED, UNKNOWN.</summary>
    public static OrderStatus MapStatus(string currentState) => currentState switch
    {
        "CREATED" => OrderStatus.Created,
        "ACCEPTED" => OrderStatus.Accepted,
        "FINISHED" => OrderStatus.Completed,
        "DENIED" => OrderStatus.Denied,
        "CANCELED" => OrderStatus.Cancelled,
        _ => OrderStatus.Unknown,
    };

    /// <summary>Pulls the Get Order URL from the orders.notification webhook body.</summary>
    public static string GetResourceHref(string notificationJson)
    {
        using var doc = JsonDocument.Parse(notificationJson);
        return doc.RootElement.GetProperty("resource_href").GetString()
            ?? throw new JsonException("resource_href is not a string");
    }
}
