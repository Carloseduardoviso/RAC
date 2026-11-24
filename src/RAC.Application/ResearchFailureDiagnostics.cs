namespace RAC.Application;
public static class ResearchFailureDiagnostics
{
    public sealed record Finding(string Reason,int Count,string ResearchDirection);
    public static IReadOnlyList<Finding> Summarize(IEnumerable<ResearchLeadershipReview.Decision> decisions)
    {
        return decisions.SelectMany(d=>d.Reasons.Distinct()).GroupBy(x=>x,StringComparer.Ordinal)
            .Select(g=>new Finding(g.Key,g.Count(),g.Key switch {
                "INSUFFICIENT_TRADES"=>"Collect additional independent observations; do not force entries",
                "INSUFFICIENT_CONSISTENCY"=>"Evaluate regime stability",
                "NONPOSITIVE_NET_RETURN"=>"Compare gross edge and execution costs",
                "UNRESOLVED_POSITIONS"=>"Investigate exit liquidity and month boundaries",
                "DRAWDOWN_LIMIT"=>"Investigate exposure and adverse excursions",
                _=>"Review evidence"
            })).OrderByDescending(x=>x.Count).ThenBy(x=>x.Reason,StringComparer.Ordinal).ToArray();
    }
}
