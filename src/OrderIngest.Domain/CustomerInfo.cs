namespace OrderIngest.Domain;

public class CustomerInfo
{
    public required string Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
