using OrderIngest.Domain;

namespace OrderIngest.Tests;

public class OrderStatusRankTests
{
    [Fact]
    public void RanksAreMonotonic_CreatedThroughTerminal()
    {
        Assert.True(OrderStatus.Created.Rank() < OrderStatus.Accepted.Rank());
        Assert.True(OrderStatus.Accepted.Rank() < OrderStatus.Completed.Rank());
    }

    [Fact]
    public void TerminalStates_ShareTopRank_SoNoneOverwritesAnother()
    {
        Assert.Equal(OrderStatus.Completed.Rank(), OrderStatus.Cancelled.Rank());
        Assert.Equal(OrderStatus.Completed.Rank(), OrderStatus.Denied.Rank());
    }

    [Fact]
    public void Unknown_RanksBelowEverything()
    {
        Assert.True(OrderStatus.Unknown.Rank() < OrderStatus.Created.Rank());
    }
}
