namespace RAC.Application;

public sealed record DanielResearchDecision(DanielMarketObservation Observation,decimal? SpreadPercent,int? Rank,string Reason)
{
    public bool TradingAuthorized => false;
}
public sealed record DanielResearchSnapshot(DateTimeOffset ObservedUtc,IReadOnlyList<DanielResearchDecision> Decisions)
{
    public bool TradingAuthorized => false;
    public static DanielResearchSnapshot Create(IEnumerable<DanielMarketObservation> observations,DateTimeOffset now)
    {
        var rows=observations.ToArray();
        if(rows.Length==0) throw new ArgumentException("Empty research universe.");
        var ranks=DanielCandidateRanking.Rank(rows,now).ToDictionary(x=>x.Symbol,StringComparer.Ordinal);
        var decisions=rows.OrderBy(x=>x.Symbol,StringComparer.Ordinal).Select(x=>
        {
            var result=DanielMarketDiscovery.Evaluate(x,now);
            var selected=ranks.GetValueOrDefault(x.Symbol);
            return new DanielResearchDecision(x,result.SpreadPercent,selected?.Rank,
                result.ResearchCandidate && selected is null ? "OUTSIDE_TOP_20" : result.Reason);
        }).ToArray();
        return new(now.ToUniversalTime(),Array.AsReadOnly(decisions));
    }
}
