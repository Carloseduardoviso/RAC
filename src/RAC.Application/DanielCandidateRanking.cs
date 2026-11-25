namespace RAC.Application;

public sealed record DanielRankedCandidate(int Rank,string Symbol,decimal QuoteVolume24h,decimal SpreadPercent)
{
    public bool TradingAuthorized => false;
}

public static class DanielCandidateRanking
{
    public static IReadOnlyList<DanielRankedCandidate> Rank(IEnumerable<DanielMarketObservation> observations,DateTimeOffset now)
    {
        var rows=observations.ToArray();
        if(rows.Select(x=>x.Symbol).Distinct(StringComparer.Ordinal).Count()!=rows.Length)
            throw new ArgumentException("Duplicate market observations.");
        return rows.Select(x=>(Data:x,Result:DanielMarketDiscovery.Evaluate(x,now)))
            .Where(x=>x.Result.ResearchCandidate)
            .OrderByDescending(x=>x.Data.QuoteVolume24h)
            .ThenBy(x=>x.Result.SpreadPercent)
            .ThenBy(x=>x.Data.Symbol,StringComparer.Ordinal)
            .Take(20)
            .Select((x,i)=>new DanielRankedCandidate(i+1,x.Data.Symbol,x.Data.QuoteVolume24h,x.Result.SpreadPercent!.Value))
            .ToArray();
    }
}
