namespace OrderIngest.Domain;

/// <summary>
/// The single internal representation of an order, regardless of which
/// provider it came from. One row per (provider, external order id).
/// All money values are integer cents in <see cref="Currency"/>.
/// </summary>
public class Order
{
    public long Id { get; set; }
    public OrderProvider Provider { get; set; }
    public required string ExternalOrderId { get; set; }
    public OrderStatus Status { get; set; }

    /// <summary>The provider's original status value, preserved for audit.</summary>
    public required string RawStatus { get; set; }

    public required CustomerInfo Customer { get; set; }
    public List<LineItem> LineItems { get; set; } = [];
    public long TotalCents { get; set; }
    public required string Currency { get; set; }

    /// <summary>The original provider payload, preserved verbatim.</summary>
    public required string RawPayload { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
}
