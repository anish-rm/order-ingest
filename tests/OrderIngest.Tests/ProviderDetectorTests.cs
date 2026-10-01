using OrderIngest.Business;
using OrderIngest.Domain;

namespace OrderIngest.Tests;

public class ProviderDetectorTests
{
    [Fact]
    public void UberNotification_IsDetectedAsUber() =>
        Assert.Equal(OrderProvider.Uber,
            ProviderDetector.Detect(TestFixtures.Read("uber-notification.json")));

    [Fact]
    public void DoorDashWebhook_IsDetectedAsDoorDash() =>
        Assert.Equal(OrderProvider.DoorDash,
            ProviderDetector.Detect(TestFixtures.Read("doordash-order.json")));

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{}")]
    [InlineData("""{"provider": "uber"}""")]
    [InlineData("""{"event": "not-an-object"}""")]
    public void UnrecognizedPayloads_AreNotDetected(string body) =>
        Assert.Null(ProviderDetector.Detect(body));
}
