namespace RAC.Application;

// Advisory monitoring only. Never changes a strategy or unlocks risk.
public static class IndependentResearchDeterioration
{
    public sealed record Assessment(string Status,IReadOnlyList<string> Reasons)
    {
        public bool TradingAuthorized=>false;
    }
    public static Assessment Evaluate(IReadOnlyList<IndependentResearchGate.Evidence> rows)
    {
        var reasons=new List<string>();
        if(rows.Count==0)return new("INSUFFICIENT_EVIDENCE",Array.Empty<string>());
        if(rows.Any(x=>x.Month.Day!=1 || x.Cost is not (.0015m or .003m) || x.Trades<0 || x.Profit<0 || x.Loss<0 || x.DrawdownPct<0 || x.DrawdownPct>100 || (x.Trades==0 && (x.Profit!=0 || x.Loss!=0))) || rows.GroupBy(x=>(x.Month,x.Cost)).Any(g=>g.Count()!=1))
            return new("INVALID_EVIDENCE",Array.AsReadOnly(new[]{"INVALID_OR_DUPLICATE_METRICS"}));
        if(rows.Any(x=>x.Unresolved || x.DrawdownPct>3))reasons.Add("RISK_REVIEW_REQUIRED");
        foreach(var group in rows.GroupBy(x=>x.Cost))
        {
            var latest=group.OrderByDescending(x=>x.Month).Take(2).ToArray();
            if(latest.Length==2 && latest[0].Month==latest[1].Month.AddMonths(1) && latest.All(x=>x.Trades>0 && x.Profit<x.Loss))
                reasons.Add("CONSECUTIVE_NEGATIVE_MONTHS");
        }
        return new(reasons.Count>0?"DETERIORATION_REVIEW_REQUIRED":"NO_ALERT",Array.AsReadOnly(reasons.Distinct().ToArray()));
    }
}
