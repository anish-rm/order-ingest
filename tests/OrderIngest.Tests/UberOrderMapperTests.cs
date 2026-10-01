using OrderIngest.Business.Mapping;
using OrderIngest.Domain;

namespace OrderIngest.Tests;

public class UberOrderMapperTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void MapsGetOrderResponseToInternalOrder()
    {
        var order = UberOrderMapper.Map(TestFixtures.Read("uber-get-order.json"), Now);

        Assert.Equal(OrderProvider.Uber, order.Provider);
        Assert.Equal("f9f363d1-e1c2-4595-b477-c649845bc953", order.ExternalOrderId);
        Assert.Equal(OrderStatus.Created, order.Status);
        Assert.Equal("CREATED", order.RawStatus);
        Assert.Equal("Larry", order.Customer.Name);
        Assert.Equal("+1 555-555-5555", order.Customer.Phone);
        Assert.Equal(1399, order.TotalCents);
        Assert.Equal("USD", order.Currency);
        Assert.Equal(Now, order.ReceivedAt);

        Assert.Collection(order.LineItems,
            muffin =>
            {
                Assert.Equal("Fresh-baked muffin", muffin.Name);
                Assert.Equal(1, muffin.Quantity);
                Assert.Equal(350, muffin.PriceCents);
            },
            coffee => Assert.Equal(300, coffee.PriceCents),
            donut => Assert.Equal("Strawberry Donut", donut.Name));
    }

    [Fact]
    public void ExtractsResourceHrefFromNotification()
    {
        var href = UberOrderMapper.GetResourceHref(TestFixtures.Read("uber-notification.json"));
        Assert.Equal(
            "https://api.uber.com/v2/eats/order/153dd7f1-339d-4619-940c-418943c14636",
            href);
    }

    [Theory]
    [InlineData("CREATED", OrderStatus.Created)]
    [InlineData("ACCEPTED", OrderStatus.Accepted)]
    [InlineData("FINISHED", OrderStatus.Completed)]
    [InlineData("DENIED", OrderStatus.Denied)]
    [InlineData("CANCELED", OrderStatus.Cancelled)]
    [InlineData("UNKNOWN", OrderStatus.Unknown)]
    [InlineData("SOMETHING_NEW", OrderStatus.Unknown)]
    public void MapsDocumentedStatusValues(string currentState, OrderStatus expected) =>
        Assert.Equal(expected, UberOrderMapper.MapStatus(currentState));
}
