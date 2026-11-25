using RAC.Application;
namespace RAC.Tests;

public sealed class DanielMarketDiscoveryTests
{
    [Fact]
    public void AcceptsResearchOnlyAndRejectsPoorQuality()
    {
        var now=DateTimeOffset.UtcNow;
        var sample=new DanielMarketObservation("ABCUSDT",true,2_000_000m,100m,100.1m,3m,now);
        var result=DanielMarketDiscovery.Evaluate(sample,now);
        Assert.True(result.ResearchCandidate);
        Assert.False(result.TradingAuthorized);
        foreach(var invalid in new[] {
            sample with { SpotTradingEnabled=false },
            sample with { QuoteVolume24h=1 },
            sample with { Ask=102 },
            sample with { Bid=0 },
            sample with { RangePercent=20 },
            sample with { ObservedUtc=now.AddMinutes(-3) },
            sample with { ObservedUtc=now.AddSeconds(1) },
            sample with { Symbol="ABCBTC" } })
        {
            var rejected=DanielMarketDiscovery.Evaluate(invalid,now);
            Assert.False(rejected.ResearchCandidate);
            Assert.False(rejected.TradingAuthorized);
        }
    }
}
