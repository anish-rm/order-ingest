namespace OrderIngest.Domain;

public enum OrderStatus
{
    Unknown = 0,
    Created,
    Accepted,
    Completed,
    Denied,
    Cancelled,
}

public static class OrderStatusExtensions
{
    /// <summary>
    /// Monotonic rank used to guard against out-of-order webhook delivery:
    /// an incoming status is applied only if its rank is strictly greater
    /// than the stored one. Terminal states share the top rank, so once an
    /// order is Completed, Denied, or Cancelled no further transition wins.
    /// </summary>
    public static int Rank(this OrderStatus status) => status switch
    {
        OrderStatus.Created => 1,
        OrderStatus.Accepted => 2,
        OrderStatus.Completed or OrderStatus.Denied or OrderStatus.Cancelled => 3,
        _ => 0, // Unknown never overwrites a known status
    };
}
