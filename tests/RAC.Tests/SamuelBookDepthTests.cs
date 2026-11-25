using RAC.Application;
namespace RAC.Tests;
public sealed class SamuelBookDepthTests
{
    [Fact]
    public void ModelsPartialDepthAndBothSides()
    {
        var buy=SamuelBookDepth.Evaluate([new(100,1),new(102,1)],3,true);
        Assert.Equal(2m,buy.Filled);
        Assert.Equal(1m,buy.Unfilled);
        Assert.Equal(202m,buy.Notional);
        Assert.Equal(101m,buy.AveragePrice);
        Assert.Equal(1m,buy.ImpactPercent);
        Assert.False(buy.FullyCovered);
        Assert.False(buy.TradingAuthorized);
        var sell=SamuelBookDepth.Evaluate([new(100,1),new(98,1)],2,false);
        Assert.Equal(99m,sell.AveragePrice);
        Assert.Equal(1m,sell.ImpactPercent);
        Assert.True(sell.FullyCovered);
        Assert.Null(SamuelBookDepth.Evaluate([],1,true).AveragePrice);
        Assert.Throws<ArgumentException>(()=>SamuelBookDepth.Evaluate([new(102,1),new(100,1)],1,true));
        Assert.Throws<ArgumentException>(()=>SamuelBookDepth.Evaluate([new(100,0)],1,true));
    }
}
