namespace OrderIngest.Domain;

public class LineItem
{
    public required string Name { get; set; }
    public int Quantity { get; set; }
    public long PriceCents { get; set; }
}
