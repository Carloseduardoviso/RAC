namespace RAC.Application;

public sealed record DanielMarketObservation(string Symbol,bool SpotTradingEnabled,
    decimal QuoteVolume24h,decimal Bid,decimal Ask,decimal RangePercent,DateTimeOffset ObservedUtc);

public sealed record DanielDiscoveryResult(string Symbol,bool ResearchCandidate,
    decimal? SpreadPercent,string Reason)
{
    public bool TradingAuthorized => false;
}

public static class DanielMarketDiscovery
{
    public static DanielDiscoveryResult Evaluate(DanielMarketObservation data,DateTimeOffset now)
    {
        DanielDiscoveryResult Reject(string reason,decimal? spread=null) =>
            new(data.Symbol,false,spread,reason);
        if (string.IsNullOrWhiteSpace(data.Symbol) || !data.Symbol.EndsWith("USDT",StringComparison.Ordinal)
            || !data.SpotTradingEnabled) return Reject("UNSUPPORTED_MARKET");
        if (data.ObservedUtc>now || now-data.ObservedUtc>TimeSpan.FromMinutes(2))
            return Reject("STALE_OR_FUTURE_DATA");
        if (data.Bid<=0 || data.Ask<data.Bid || data.QuoteVolume24h<0 || data.RangePercent<0)
            return Reject("INVALID_DATA");
        var spread=(data.Ask-data.Bid)/data.Ask*100m;
        if (data.QuoteVolume24h<1_000_000m) return Reject("LOW_QUOTE_VOLUME",spread);
        if (spread>0.2m) return Reject("WIDE_SPREAD",spread);
        if (data.RangePercent<0.5m || data.RangePercent>15m) return Reject("RANGE_OUTSIDE_RESEARCH_SCREEN",spread);
        return new(data.Symbol,true,spread,"RESEARCH_ONLY");
    }
}
