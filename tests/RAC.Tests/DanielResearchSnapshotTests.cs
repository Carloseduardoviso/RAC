using RAC.Application;
namespace RAC.Tests;
public sealed class DanielResearchSnapshotTests
{
    [Fact]
    public void PreservesEveryObservationAndDeterministicExclusion()
    {
        var now=DateTimeOffset.UtcNow;
        var rows=Enumerable.Range(0,21).Select(i=>new DanielMarketObservation($"ASSET{i:D2}USDT",true,2_000_000m+i,100m,100.1m,3m,now)).ToList();
        rows.Add(rows[0] with {Symbol="STALEUSDT",ObservedUtc=now.AddMinutes(-3)});
        rows.Add(rows[0] with {Symbol="LOWUSDT",QuoteVolume24h=1});
        var snapshot=DanielResearchSnapshot.Create(rows,now);
        Assert.Equal(23,snapshot.Decisions.Count);
        Assert.Equal(20,snapshot.Decisions.Count(x=>x.Rank.HasValue));
        Assert.Equal("OUTSIDE_TOP_20",snapshot.Decisions.Single(x=>x.Observation.Symbol=="ASSET00USDT").Reason);
        Assert.Equal("STALE_OR_FUTURE_DATA",snapshot.Decisions.Single(x=>x.Observation.Symbol=="STALEUSDT").Reason);
        Assert.Equal("LOW_QUOTE_VOLUME",snapshot.Decisions.Single(x=>x.Observation.Symbol=="LOWUSDT").Reason);
        Assert.Equal(snapshot.Decisions.ToArray(),DanielResearchSnapshot.Create(rows.AsEnumerable().Reverse(),now).Decisions.ToArray());
        Assert.False(snapshot.TradingAuthorized);
        Assert.All(snapshot.Decisions,x=>Assert.False(x.TradingAuthorized));
    }
    [Fact]
    public void RejectsEmptyAndDuplicateUniverses()
    {
        var now=DateTimeOffset.UtcNow;
        var row=new DanielMarketObservation("BTCUSDT",true,2_000_000m,100m,100.1m,3m,now);
        Assert.Throws<ArgumentException>(()=>DanielResearchSnapshot.Create([],now));
        Assert.Throws<ArgumentException>(()=>DanielResearchSnapshot.Create([row,row],now));
    }
}
