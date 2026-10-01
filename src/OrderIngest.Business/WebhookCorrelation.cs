using System.Text.Json;
using OrderIngest.Domain;

namespace OrderIngest.Business;

public static class WebhookCorrelation
{
    /// <summary>
    /// Pulls a provider-native id out of the raw webhook for log
    /// correlation: Uber's event_id, DoorDash's order id. Never throws —
    /// a missing id degrades logs, not processing.
    /// </summary>
    public static string? Extract(OrderProvider provider, string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            return provider switch
            {
                OrderProvider.Uber when root.TryGetProperty("event_id", out var id) =>
                    id.GetString(),
                OrderProvider.DoorDash when root.TryGetProperty("order", out var order)
                    && order.ValueKind == JsonValueKind.Object
                    && order.TryGetProperty("id", out var id) => id.GetString(),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
