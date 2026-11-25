using RAC.Application;
namespace RAC.Tests;

public sealed class DanielCandidateRankingTests
{
    [Fact]
    public void RanksOnlyEligibleMarketsDeterministically()
    {
        var now=DateTimeOffset.UtcNow;
        var a=new DanielMarketObservation("AAAUSDT",true,2_000_000m,100m,100.1m,3m,now);
        var b=a with { Symbol="BBBUSDT",QuoteVolume24h=3_000_000m };
        var c=b with { Symbol="CCCUSDT",Ask=100.05m };
        var d=c with { Symbol="DDDUSDT",ObservedUtc=now.AddMinutes(-3) };
        var result=DanielCandidateRanking.Rank(new[]{a,b,c,d},now);
        Assert.Equal(new[]{"CCCUSDT","BBBUSDT","AAAUSDT"},result.Select(x=>x.Symbol));
        Assert.All(result,x=>Assert.False(x.TradingAuthorized));
        Assert.Throws<ArgumentException>(()=>DanielCandidateRanking.Rank(new[]{a,a},now));
    }
}
