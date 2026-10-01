using System.Text.Json;
using OrderIngest.Domain;

namespace OrderIngest.Business;

public static class ProviderDetector
{
    /// <summary>
    /// Detects the provider from the payload shape:
    /// a root "event_type" equal to "orders.notification" marks Uber
    /// a root "event" object with a "type" field marks DoorDash.
    /// Returns null when neither shape matches or the body is not JSON.
    /// </summary>
    public static OrderProvider? Detect(string rawBody)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawBody);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty("event_type", out var eventType)
                && eventType.ValueKind == JsonValueKind.String
                && eventType.GetString() == "orders.notification")
            {
                return OrderProvider.Uber;
            }

            if (root.TryGetProperty("event", out var @event)
                && @event.ValueKind == JsonValueKind.Object
                && @event.TryGetProperty("type", out _))
            {
                return OrderProvider.DoorDash;
            }

            return null;
        }
    }
}
