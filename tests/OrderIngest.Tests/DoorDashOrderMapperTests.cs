using OrderIngest.Business.Mapping;
using OrderIngest.Domain;

namespace OrderIngest.Tests;

public class DoorDashOrderMapperTests
{
    [Fact]
    public void MapsWebhookToInternalOrder()
    {
        var order = DoorDashOrderMapper.Map(TestFixtures.Read("doordash-order.json"), DateTimeOffset.UtcNow);

        Assert.Equal(OrderProvider.DoorDash, order.Provider);
        Assert.Equal("abc12345", order.ExternalOrderId);
        Assert.Equal(OrderStatus.Created, order.Status);
        Assert.Equal("OrderCreate/NEW", order.RawStatus);
        Assert.Equal("Kelley W.", order.Customer.Name);
        Assert.Equal("support@doordash.com", order.Customer.Email);
        Assert.Equal("+18559731040", order.Customer.Phone);

        // subtotal 2000 + tax 300
        Assert.Equal(2300, order.TotalCents);
        Assert.Equal("USD", order.Currency);
    }

    [Fact]
    public void FlattensItemsAcrossCategories()
    {
        var order = DoorDashOrderMapper.Map(TestFixtures.Read("doordash-order.json"), DateTimeOffset.UtcNow);

        var item = Assert.Single(order.LineItems);
        Assert.Equal("Burrito Scram-Bowl", item.Name);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(0, item.PriceCents);
    }

    [Fact]
    public void UnknownEventType_MapsToUnknownStatus() =>
        Assert.Equal(OrderStatus.Unknown, DoorDashOrderMapper.MapStatus("SomethingElse", "NEW"));
}
