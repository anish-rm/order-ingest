using System.Threading.Channels;
using OrderIngest.Domain;

namespace OrderIngest.Business;

/// <param name="CorrelationId">Uber's event_id or DoorDash's order id — ties
/// processing logs back to the received webhook.</param>
public record WebhookWorkItem(OrderProvider Provider, string RawBody, string? CorrelationId);

/// <summary>
/// Decouples the webhook endpoint from processing so the endpoint can
/// acknowledge with 200 immediately, as Uber's contract requires.
/// Bounded so a flood of webhooks applies backpressure instead of
/// growing memory without limit.
/// </summary>
public class WebhookQueue
{
    private readonly Channel<WebhookWorkItem> _channel =
        Channel.CreateBounded<WebhookWorkItem>(capacity: 100);

    public ValueTask EnqueueAsync(WebhookWorkItem item, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(item, ct);

    public IAsyncEnumerable<WebhookWorkItem> ReadAllAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);

    public void Complete() => _channel.Writer.TryComplete();
}
