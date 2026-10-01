using System.Text.Json;
using OrderIngest.Domain;

namespace OrderIngest.Business;

public static class ProviderDetector
{
    /// <summary>
    /// Detects the provider from the payload shape alone:
    /// a root "event_type" string marks Uber (orders.notification),
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
                && eventType.ValueKind == JsonValueKind.String)
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
